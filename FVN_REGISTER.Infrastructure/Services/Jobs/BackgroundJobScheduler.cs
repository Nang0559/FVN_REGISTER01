using System.Collections.Concurrent;
using System.Threading.Channels;
using FVN_REGISTER.Application.Interfaces.Jobs;
using FVN_REGISTER.Contract.Dtos.Jobs;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Core.Constants;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FVN_REGISTER.Infrastructure.Services.Jobs;

/// <summary>
/// Reads/writes dbo.F03BackgroundJobSchedules (SQL/75) and lets hosted workers follow the
/// schedule configured in the admin UI. Singleton: it keeps a short in-memory cache so the
/// eight workers together cost at most one tiny SELECT every few seconds.
///
/// Failure policy: if the table is missing or SQL is unavailable the workers fall back to the
/// defaults in <see cref="BackgroundJobCatalog"/> (or the last good cache) and keep running.
/// </summary>
public sealed class BackgroundJobScheduler : IBackgroundJobScheduler, IBackgroundJobScheduleService
{
    private const string Table = "dbo.F03BackgroundJobSchedules";

    private const string SelectSql =
        "SELECT JobKey, DisplayName, Description, IsEnabled, IntervalMinutes, DefaultIntervalMinutes, RunRequested, " +
        "LastStartedAt, LastFinishedAt, LastStatus, LastDurationMs, LastMessage, ModifiedAt " +
        "FROM " + Table + ";";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollSlice = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundJobScheduler> _logger;

    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly ConcurrentDictionary<string, DateTime> _startedAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly Channel<(string Sql, Action<SqlParameterCollection> Configure)> _writes =
        Channel.CreateUnbounded<(string, Action<SqlParameterCollection>)>();

    private Dictionary<string, ScheduleRow> _cache = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _cacheLoadedUtc = DateTime.MinValue;
    private volatile bool _lastLoadFailed;
    private string? _connectionString;
    private DateTime _lastWarnUtc = DateTime.MinValue;
    private int _writerStarted;

    public BackgroundJobScheduler(IServiceScopeFactory scopeFactory, ILogger<BackgroundJobScheduler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // ------------------------------------------------------------------
    // IBackgroundJobScheduler (used by workers)
    // ------------------------------------------------------------------

    public async Task WaitUntilEnabledAsync(string jobKey, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var row = await GetAsync(jobKey, ct);
            if (row.IsEnabled) return;
            await Task.Delay(PollSlice, ct);
        }
    }

    public async Task WaitForNextAsync(string jobKey, CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        var jitterSeconds = -1;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var row = await GetAsync(jobKey, ct);

            if (!row.IsEnabled)
            {
                await Task.Delay(PollSlice, ct);
                continue;
            }

            if (row.RunRequested)
            {
                row.RunRequested = false; // local guard so we never fire twice for one request
                Enqueue($"UPDATE {Table} SET RunRequested = 0 WHERE JobKey = @k;", p => Param(p, "@k", jobKey));
                _cacheLoadedUtc = DateTime.MinValue;
                return;
            }

            // Jitter is picked once per wait so that workers sharing the same interval drift apart
            // instead of all hitting SQL at the same minute mark (max 10% of interval, <= 60s).
            if (jitterSeconds < 0)
                jitterSeconds = Random.Shared.Next(0, Math.Min(60, Math.Max(1, row.IntervalMinutes * 6)) + 1);

            var due = TimeSpan.FromMinutes(row.IntervalMinutes) + TimeSpan.FromSeconds(jitterSeconds);
            var remaining = due - (DateTime.UtcNow - started);
            if (remaining <= TimeSpan.Zero) return;

            await Task.Delay(remaining < PollSlice ? remaining : PollSlice, ct);
        }
    }

    public void MarkStarted(string jobKey)
    {
        var now = DateTime.Now;
        _startedAt[jobKey] = now;
        Enqueue(
            $"UPDATE {Table} SET LastStartedAt = @now, LastStatus = N'Running', LastMessage = NULL WHERE JobKey = @k;",
            p => { Param(p, "@now", now); Param(p, "@k", jobKey); });
    }

    public void MarkSucceeded(string jobKey, string? message = null) => Finish(jobKey, "Success", message);

    public void MarkFailed(string jobKey, string message) => Finish(jobKey, "Failed", message);

    private void Finish(string jobKey, string status, string? message)
    {
        var now = DateTime.Now;
        int? durationMs = _startedAt.TryRemove(jobKey, out var started)
            ? (int)Math.Min(int.MaxValue, Math.Max(0, (now - started).TotalMilliseconds))
            : null;
        var text = Truncate(message, 500);

        Enqueue(
            $"UPDATE {Table} SET LastFinishedAt = @now, LastStatus = @st, LastDurationMs = @dur, LastMessage = @msg WHERE JobKey = @k;",
            p =>
            {
                Param(p, "@now", now);
                Param(p, "@st", status);
                Param(p, "@dur", durationMs);
                Param(p, "@msg", text);
                Param(p, "@k", jobKey);
            });
    }

    // ------------------------------------------------------------------
    // IBackgroundJobScheduleService (used by the admin API)
    // ------------------------------------------------------------------

    public async Task<ServiceResult<List<BackgroundJobScheduleDto>>> GetAllAsync(CancellationToken ct = default)
    {
        try
        {
            var rows = await LoadAsync(force: true, ct);
            var list = BackgroundJobCatalog.All
                .Select(def => ToDto(rows.TryGetValue(def.Key, out var r) ? r : DefaultRow(def), def))
                .ToList();

            return _lastLoadFailed
                ? ServiceResult<List<BackgroundJobScheduleDto>>.Ok(list,
                    "Chưa đọc được bảng F03BackgroundJobSchedules (chạy SQL/75_BackgroundJobSchedules.sql). Đang dùng lịch mặc định.")
                : ServiceResult<List<BackgroundJobScheduleDto>>.Ok(list);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[JOB-SCHEDULE] GetAll failed.");
            return ServiceResult<List<BackgroundJobScheduleDto>>.Fail("Không tải được lịch chạy background job.");
        }
    }

    public async Task<ServiceResult<BackgroundJobScheduleDto>> UpdateAsync(
        string jobKey, UpdateBackgroundJobScheduleRequest request, int? modifiedBy, CancellationToken ct = default)
    {
        var def = BackgroundJobCatalog.Find(jobKey);
        if (def == null)
            return ServiceResult<BackgroundJobScheduleDto>.Fail("Job không tồn tại.");

        if (request.IntervalMinutes < BackgroundJobCatalog.MinIntervalMinutes
            || request.IntervalMinutes > BackgroundJobCatalog.MaxIntervalMinutes)
            return ServiceResult<BackgroundJobScheduleDto>.Fail(
                $"Chu kỳ phải từ {BackgroundJobCatalog.MinIntervalMinutes} đến {BackgroundJobCatalog.MaxIntervalMinutes} phút.");

        if (!request.IsEnabled && !def.AllowDisable)
            return ServiceResult<BackgroundJobScheduleDto>.Fail("Đây là tác vụ bảo mật nên không được tắt.");

        return await WriteAsync(def, ct, async () => await ExecuteAsync(
            $"UPDATE {Table} SET IsEnabled = @en, IntervalMinutes = @iv, ModifiedBy = @by, ModifiedAt = @now WHERE JobKey = @k;",
            p =>
            {
                Param(p, "@en", request.IsEnabled);
                Param(p, "@iv", request.IntervalMinutes);
                Param(p, "@by", modifiedBy);
                Param(p, "@now", DateTime.Now);
                Param(p, "@k", def.Key);
            }, ct));
    }

    public async Task<ServiceResult> RequestRunNowAsync(string jobKey, int? modifiedBy, CancellationToken ct = default)
    {
        var def = BackgroundJobCatalog.Find(jobKey);
        if (def == null)
            return ServiceResult.Fail("Job không tồn tại.");

        try
        {
            var rows = await LoadAsync(force: true, ct);
            if (rows.TryGetValue(def.Key, out var row) && !row.IsEnabled)
                return ServiceResult.Fail("Job đang tắt. Hãy bật job trước khi chạy ngay.");

            var affected = await ExecuteAsync(
                $"UPDATE {Table} SET RunRequested = 1, ModifiedBy = @by, ModifiedAt = @now WHERE JobKey = @k;",
                p => { Param(p, "@by", modifiedBy); Param(p, "@now", DateTime.Now); Param(p, "@k", def.Key); }, ct);

            Invalidate();
            return affected > 0
                ? ServiceResult.Ok("Đã yêu cầu chạy ngay. Job sẽ chạy trong vòng ~15 giây.")
                : ServiceResult.Fail("Chưa có cấu hình cho job này (chạy SQL/75_BackgroundJobSchedules.sql).");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[JOB-SCHEDULE] RunNow failed for {Job}.", def.Key);
            return ServiceResult.Fail(DescribeFailure(ex));
        }
    }

    public async Task<ServiceResult<BackgroundJobScheduleDto>> ResetToDefaultAsync(
        string jobKey, int? modifiedBy, CancellationToken ct = default)
    {
        var def = BackgroundJobCatalog.Find(jobKey);
        if (def == null)
            return ServiceResult<BackgroundJobScheduleDto>.Fail("Job không tồn tại.");

        return await WriteAsync(def, ct, async () => await ExecuteAsync(
            $"UPDATE {Table} SET IsEnabled = 1, IntervalMinutes = @iv, DefaultIntervalMinutes = @iv, ModifiedBy = @by, ModifiedAt = @now WHERE JobKey = @k;",
            p =>
            {
                Param(p, "@iv", def.DefaultIntervalMinutes);
                Param(p, "@by", modifiedBy);
                Param(p, "@now", DateTime.Now);
                Param(p, "@k", def.Key);
            }, ct));
    }

    private async Task<ServiceResult<BackgroundJobScheduleDto>> WriteAsync(
        BackgroundJobDefinition def, CancellationToken ct, Func<Task<int>> update)
    {
        try
        {
            await LoadAsync(force: true, ct); // makes sure the row exists (auto-register)
            var affected = await update();
            Invalidate();

            if (affected == 0)
                return ServiceResult<BackgroundJobScheduleDto>.Fail(
                    "Chưa có cấu hình cho job này (chạy SQL/75_BackgroundJobSchedules.sql).");

            var rows = await LoadAsync(force: true, ct);
            return ServiceResult<BackgroundJobScheduleDto>.Ok(
                ToDto(rows.TryGetValue(def.Key, out var r) ? r : DefaultRow(def), def),
                "Đã lưu. Worker áp dụng lịch mới trong vòng ~15 giây.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[JOB-SCHEDULE] Update failed for {Job}.", def.Key);
            return ServiceResult<BackgroundJobScheduleDto>.Fail(DescribeFailure(ex));
        }
    }

    // ------------------------------------------------------------------
    // Cache + SQL helpers
    // ------------------------------------------------------------------

    private sealed class ScheduleRow
    {
        public string JobKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsEnabled { get; set; } = true;
        public int IntervalMinutes { get; set; } = 30;
        public int DefaultIntervalMinutes { get; set; } = 30;
        public bool RunRequested { get; set; }
        public DateTime? LastStartedAt { get; set; }
        public DateTime? LastFinishedAt { get; set; }
        public string? LastStatus { get; set; }
        public int? LastDurationMs { get; set; }
        public string? LastMessage { get; set; }
        public DateTime? ModifiedAt { get; set; }
    }

    private static ScheduleRow DefaultRow(BackgroundJobDefinition def) => new()
    {
        JobKey = def.Key,
        DisplayName = def.DisplayName,
        Description = def.Description,
        IsEnabled = true,
        IntervalMinutes = def.DefaultIntervalMinutes,
        DefaultIntervalMinutes = def.DefaultIntervalMinutes
    };

    private async Task<ScheduleRow> GetAsync(string jobKey, CancellationToken ct)
    {
        var rows = await LoadAsync(force: false, ct);
        if (rows.TryGetValue(jobKey, out var row)) return row;

        var def = BackgroundJobCatalog.Find(jobKey)
                  ?? new BackgroundJobDefinition(jobKey, jobKey, string.Empty, 30);
        return DefaultRow(def);
    }

    private void Invalidate() => _cacheLoadedUtc = DateTime.MinValue;

    private async Task<Dictionary<string, ScheduleRow>> LoadAsync(bool force, CancellationToken ct)
    {
        if (!force && DateTime.UtcNow - _cacheLoadedUtc < CacheTtl) return _cache;

        await _loadLock.WaitAsync(ct);
        try
        {
            if (!force && DateTime.UtcNow - _cacheLoadedUtc < CacheTtl) return _cache;

            Dictionary<string, ScheduleRow> rows;
            try
            {
                rows = new Dictionary<string, ScheduleRow>(StringComparer.OrdinalIgnoreCase);

                await using var conn = new SqlConnection(ConnectionString());
                await conn.OpenAsync(ct);

                await using (var cmd = new SqlCommand(SelectSql, conn) { CommandTimeout = 10 })
                await using (var reader = await cmd.ExecuteReaderAsync(ct))
                {
                    while (await reader.ReadAsync(ct))
                    {
                        var row = ReadRow(reader);
                        rows[row.JobKey] = row;
                    }
                }

                // Auto-register catalog jobs missing from the table (e.g. a job added in a later release).
                foreach (var def in BackgroundJobCatalog.All.Where(d => !rows.ContainsKey(d.Key)))
                {
                    await using var insert = new SqlCommand(
                        $"IF NOT EXISTS (SELECT 1 FROM {Table} WHERE JobKey = @k) " +
                        $"INSERT INTO {Table} (JobKey, DisplayName, Description, IsEnabled, IntervalMinutes, DefaultIntervalMinutes) " +
                        "VALUES (@k, @n, @d, 1, @i, @i);", conn) { CommandTimeout = 10 };
                    Param(insert.Parameters, "@k", def.Key);
                    Param(insert.Parameters, "@n", def.DisplayName);
                    Param(insert.Parameters, "@d", def.Description);
                    Param(insert.Parameters, "@i", def.DefaultIntervalMinutes);
                    await insert.ExecuteNonQueryAsync(ct);
                    rows[def.Key] = DefaultRow(def);
                }

                _lastLoadFailed = false;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _lastLoadFailed = true;
                WarnThrottled(ex);

                // Keep the last good configuration; before any success use the code defaults.
                rows = _cache.Count > 0
                    ? _cache
                    : BackgroundJobCatalog.All.ToDictionary(d => d.Key, DefaultRow, StringComparer.OrdinalIgnoreCase);
            }

            _cache = rows;
            _cacheLoadedUtc = DateTime.UtcNow;
            return rows;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private static ScheduleRow ReadRow(SqlDataReader r)
    {
        var key = r.GetString(0);
        var def = BackgroundJobCatalog.Find(key);

        var interval = r.GetInt32(4);
        interval = Math.Clamp(interval, BackgroundJobCatalog.MinIntervalMinutes, BackgroundJobCatalog.MaxIntervalMinutes);

        return new ScheduleRow
        {
            JobKey = key,
            DisplayName = r.IsDBNull(1) ? (def?.DisplayName ?? key) : r.GetString(1),
            Description = r.IsDBNull(2) ? def?.Description : r.GetString(2),
            // Housekeeping jobs can never be disabled, even if someone edits the table by hand.
            IsEnabled = r.GetBoolean(3) || def is { AllowDisable: false },
            IntervalMinutes = interval,
            DefaultIntervalMinutes = r.GetInt32(5),
            RunRequested = r.GetBoolean(6),
            LastStartedAt = r.IsDBNull(7) ? null : r.GetDateTime(7),
            LastFinishedAt = r.IsDBNull(8) ? null : r.GetDateTime(8),
            LastStatus = r.IsDBNull(9) ? null : r.GetString(9),
            LastDurationMs = r.IsDBNull(10) ? null : r.GetInt32(10),
            LastMessage = r.IsDBNull(11) ? null : r.GetString(11),
            ModifiedAt = r.IsDBNull(12) ? null : r.GetDateTime(12)
        };
    }

    private static BackgroundJobScheduleDto ToDto(ScheduleRow row, BackgroundJobDefinition def) => new()
    {
        JobKey = row.JobKey,
        DisplayName = string.IsNullOrWhiteSpace(row.DisplayName) ? def.DisplayName : row.DisplayName,
        Description = row.Description ?? def.Description,
        IsEnabled = row.IsEnabled,
        IntervalMinutes = row.IntervalMinutes,
        DefaultIntervalMinutes = def.DefaultIntervalMinutes,
        AllowDisable = def.AllowDisable,
        RunRequested = row.RunRequested,
        LastStartedAt = row.LastStartedAt,
        LastFinishedAt = row.LastFinishedAt,
        LastStatus = row.LastStatus,
        LastDurationMs = row.LastDurationMs,
        LastMessage = row.LastMessage,
        ModifiedAt = row.ModifiedAt,
        NextRunAt = row.IsEnabled
                    && row.LastFinishedAt.HasValue
                    && !string.Equals(row.LastStatus, "Running", StringComparison.OrdinalIgnoreCase)
            ? row.LastFinishedAt.Value.AddMinutes(row.IntervalMinutes)
            : null
    };

    private string ConnectionString()
    {
        if (_connectionString != null) return _connectionString;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FVNWEBAPPContext>();
        _connectionString = db.Database.GetConnectionString()
                            ?? throw new InvalidOperationException("FVNWEBAPPContext has no connection string.");
        return _connectionString;
    }

    private async Task<int> ExecuteAsync(string sql, Action<SqlParameterCollection> configure, CancellationToken ct)
    {
        await using var conn = new SqlConnection(ConnectionString());
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 10 };
        configure(cmd.Parameters);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    // Status writes go through ONE consumer so "Running" can never overwrite a later "Success".
    private void Enqueue(string sql, Action<SqlParameterCollection> configure)
    {
        _writes.Writer.TryWrite((sql, configure));
        if (Interlocked.Exchange(ref _writerStarted, 1) == 0)
            _ = Task.Run(WriterLoopAsync);
    }

    private async Task WriterLoopAsync()
    {
        await foreach (var (sql, configure) in _writes.Reader.ReadAllAsync())
        {
            try
            {
                await ExecuteAsync(sql, configure, CancellationToken.None);
            }
            catch (Exception ex)
            {
                WarnThrottled(ex);
            }
        }
    }

    private static void Param(SqlParameterCollection p, string name, object? value)
        => p.AddWithValue(name, value ?? DBNull.Value);

    private static string? Truncate(string? text, int max)
        => string.IsNullOrEmpty(text) ? text : text.Length <= max ? text : text[..max];

    private static string DescribeFailure(Exception ex)
        => ex is SqlException { Number: 208 }
            ? "Chưa có bảng F03BackgroundJobSchedules (chạy SQL/75_BackgroundJobSchedules.sql)."
            : "Lỗi hệ thống khi lưu lịch chạy.";

    private void WarnThrottled(Exception ex)
    {
        if (DateTime.UtcNow - _lastWarnUtc < TimeSpan.FromMinutes(1)) return;
        _lastWarnUtc = DateTime.UtcNow;
        _logger.LogWarning(ex,
            "[JOB-SCHEDULE] Schedule table unavailable; workers use default/last-known schedule.");
    }
}
