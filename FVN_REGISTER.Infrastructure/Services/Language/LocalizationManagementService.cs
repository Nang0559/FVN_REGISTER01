using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using FVN_REGISTER.Application.Interfaces.Language;
using FVN_REGISTER.Contract.Dtos.Language;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace FVN_REGISTER.Infrastructure.Services.Language;

public sealed class LocalizationManagementService : ILocalizationManagementService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly Regex AttributeLiteral = new(
        @"\b(?<name>Label|Text|Title|Placeholder|HelperText|ToolTip|Tooltip|AriaLabel|Description)\s*=\s*""(?<value>[^""\r\n]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TagLiteral = new(
        @"<(?<tag>MudButton|MudMenuItem|MudText|MudAlert|MudChip|MudTabPanel|PageTitle)[^>]*>\s*(?<value>[^<@\r\n]+)<",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SnackbarLiteral = new(
        @"Snackbar\.Add\(\s*""(?<value>[^""\r\n]+)""",
        RegexOptions.Compiled);

    private static readonly Regex LanguageKey = new(
        @"Language\.T\(\s*""(?<key>[^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex StoreKey = new(
        @"LocalizationStore\.Get\([^,]+,\s*""(?<key>[^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex Placeholder = new(@"\{(\d+)\}", RegexOptions.Compiled);

    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public LocalizationManagementService(IHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _configuration = configuration;
    }

    public Task<LocalizationCatalogDto> GetCatalogAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var catalog = ReadCatalog();
        return Task.FromResult(catalog);
    }

    public Task UpsertAsync(LocalizationUpsertRequest request, CancellationToken ct = default)
    {
        ValidateEntry(request.Key, request.Module, request.Vi, request.Ja);
        ValidatePlaceholders(request.Vi, request.Ja);

        var root = RequireLocalizationRoot();
        var all = ReadRawCatalog(root);
        var oldModule = all.FirstOrDefault(x => string.Equals(x.Key, request.Key.Trim(), StringComparison.OrdinalIgnoreCase))?.Module;

        if (!string.IsNullOrWhiteSpace(oldModule) && !string.Equals(oldModule, request.Module.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            RemoveKey(root, oldModule, request.Key.Trim());
        }

        WriteEntry(root, request.Module.Trim(), request.Key.Trim(), request.Vi, request.Ja);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Key không được để trống.");

        var root = RequireLocalizationRoot();
        var entries = ReadRawCatalog(root).Where(x => string.Equals(x.Key, key.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        if (entries.Count == 0)
            throw new InvalidOperationException($"Không tìm thấy key '{key}'.");

        foreach (var item in entries)
            RemoveKey(root, item.Module, item.Key);

        return Task.CompletedTask;
    }

    public async Task<(byte[] Content, string FileName)> ExportAsync(CancellationToken ct = default)
    {
        var catalog = ReadCatalog();
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Localization");
        var headers = new[] { "Key", "Module", "VI", "JA" };

        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];

        var row = 2;
        foreach (var entry in catalog.Entries.OrderBy(x => x.Module).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            ws.Cell(row, 1).Value = entry.Key;
            ws.Cell(row, 2).Value = entry.Module;
            ws.Cell(row, 3).Value = entry.Vi;
            ws.Cell(row, 4).Value = entry.Ja;
            row++;
        }

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
        ws.Column(3).Width = Math.Min(ws.Column(3).Width, 80);
        ws.Column(4).Width = Math.Min(ws.Column(4).Width, 80);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return (stream.ToArray(), $"FVN_Localization_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
    }

    public async Task<LocalizationImportResultDto> ImportAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) &&
            !fileName.EndsWith(".xlsm", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ hỗ trợ file Excel .xlsx hoặc .xlsm.");

        using var workbook = new XLWorkbook(content);
        var ws = workbook.Worksheets.FirstOrDefault()
                 ?? throw new InvalidOperationException("File Excel không có sheet.");

        var headers = ws.Row(1).CellsUsed().ToDictionary(
            x => x.GetString().Trim(),
            x => x.Address.ColumnNumber,
            StringComparer.OrdinalIgnoreCase);

        if (!headers.TryGetValue("Key", out var keyCol))
            throw new InvalidOperationException("Thiếu cột 'Key'. File phải có: Key, Module, VI, JA.");

        headers.TryGetValue("Module", out var moduleCol);
        headers.TryGetValue("VI", out var viCol);
        headers.TryGetValue("JA", out var jaCol);

        if (viCol == 0 || jaCol == 0)
            throw new InvalidOperationException("Thiếu cột 'VI' hoặc 'JA'.");

        var rows = new List<LocalizationUpsertRequest>();
        var result = new LocalizationImportResultDto();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in ws.RowsUsed().Skip(1))
        {
            ct.ThrowIfCancellationRequested();
            var key = row.Cell(keyCol).GetString().Trim();
            if (string.IsNullOrWhiteSpace(key))
                continue;

            var module = moduleCol > 0 ? row.Cell(moduleCol).GetString().Trim() : string.Empty;
            var vi = row.Cell(viCol).GetString();
            var ja = row.Cell(jaCol).GetString();

            if (!seen.Add(key))
            {
                result.Errors.Add($"Dòng {row.RowNumber()}: trùng Key '{key}'.");
                continue;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(module))
                    module = InferModule(key);
                ValidateEntry(key, module, vi, ja);
                ValidatePlaceholders(vi, ja);
                rows.Add(new LocalizationUpsertRequest { Key = key, Module = module, Vi = vi, Ja = ja });
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Dòng {row.RowNumber()}: {ex.Message}");
            }
        }

        if (result.Errors.Count > 0)
            return result;

        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            var existing = ReadRawCatalog(RequireLocalizationRoot())
                .FirstOrDefault(x => string.Equals(x.Key, row.Key, StringComparison.OrdinalIgnoreCase));
            await UpsertAsync(row, ct);
            result.Imported++;
            if (existing is null) result.Created++; else result.Updated++;
        }

        return result;
    }

    public Task<LocalizationAuditResultDto> AuditAsync(CancellationToken ct = default)
    {
        var result = new LocalizationAuditResultDto { ScannedAt = DateTime.Now };
        var root = ResolveLocalizationRoot();
        var sourceRoot = ResolveSharedRoot();

        result.SourceRoot = sourceRoot;
        result.SourceAvailable = Directory.Exists(sourceRoot);

        var catalog = ReadRawCatalog(root);
        var byKey = catalog
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var item in byKey.Values.SelectMany(x => x).GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var vi = item.FirstOrDefault(x => x.Language == "vi")?.Value;
            var ja = item.FirstOrDefault(x => x.Language == "ja")?.Value;

            if (string.IsNullOrWhiteSpace(vi))
                Add(result, "MissingVi", "Catalog", null, item.Key, null, null, vi, ja, $"Thiếu bản dịch VI cho key '{item.Key}'.");
            if (string.IsNullOrWhiteSpace(ja))
                Add(result, "MissingJa", "Catalog", null, item.Key, null, null, vi, ja, $"Thiếu bản dịch JA cho key '{item.Key}'.");
            if (!string.IsNullOrWhiteSpace(vi) && !string.IsNullOrWhiteSpace(ja) &&
                string.Equals(vi.Trim(), ja.Trim(), StringComparison.Ordinal))
                Add(result, "SameValue", "Catalog", null, item.Key, null, null, vi, ja, "VI và JA đang có cùng nội dung.");

            if (!PlaceholderSet(vi).SequenceEqual(PlaceholderSet(ja)))
                Add(result, "PlaceholderMismatch", "Catalog", null, item.Key, null, null, vi, ja, "VI và JA có placeholder {0}, {1}... không giống nhau.");
        }

        var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(sourceRoot))
        {
            foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.*", SearchOption.AllDirectories)
                         .Where(x => x.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                                  || x.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
            {
                if (IsExcludedSource(file)) continue;
                ct.ThrowIfCancellationRequested();
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    foreach (Match m in LanguageKey.Matches(line))
                    {
                        var key = m.Groups["key"].Value.Trim();
                        usedKeys.Add(key);
                        if (!byKey.ContainsKey(key))
                            Add(result, "MissingKey", Relative(sourceRoot, file), i + 1, key, null, null, null, null, $"Key '{key}' chưa được định nghĩa.");
                    }
                    foreach (Match m in StoreKey.Matches(line))
                        usedKeys.Add(m.Groups["key"].Value.Trim());

                    ScanHardcoded(result, sourceRoot, file, i + 1, line, byKey);
                }
            }

            foreach (var entry in catalog.Where(x => !usedKeys.Contains(x.Key)).GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                Add(result, "UnusedKey", "Catalog", null, entry.Key, null, null,
                    entry.FirstOrDefault(x => x.Language == "vi")?.Value,
                    entry.FirstOrDefault(x => x.Language == "ja")?.Value,
                    $"Key '{entry.Key}' không được phát hiện đang sử dụng trong UI source.");
        }

        result.Findings = result.Findings
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x.Type).ThenBy(x => x.SourceFile).ThenBy(x => x.Line)
            .ToList();

        result.Summary = new LocalizationAuditSummaryDto
        {
            Total = result.Findings.Count,
            MissingKey = result.Findings.Count(x => x.Type == "MissingKey"),
            HardCodedUi = result.Findings.Count(x => x.Type == "HardCodedUi"),
            MissingVi = result.Findings.Count(x => x.Type == "MissingVi"),
            MissingJa = result.Findings.Count(x => x.Type == "MissingJa"),
            SameValue = result.Findings.Count(x => x.Type == "SameValue"),
            UnusedKey = result.Findings.Count(x => x.Type == "UnusedKey"),
            PlaceholderMismatch = result.Findings.Count(x => x.Type == "PlaceholderMismatch")
        };

        return Task.FromResult(result);
    }

    private void ScanHardcoded(
        LocalizationAuditResultDto result,
        string sourceRoot,
        string file,
        int line,
        string text,
        IReadOnlyDictionary<string, List<RawEntry>> byKey)
    {
        foreach (Match m in AttributeLiteral.Matches(text))
            AddHardcoded(result, sourceRoot, file, line, m.Groups["value"].Value, m.Groups["name"].Value, byKey);

        foreach (Match m in TagLiteral.Matches(text))
            AddHardcoded(result, sourceRoot, file, line, m.Groups["value"].Value, m.Groups["tag"].Value, byKey);

        foreach (Match m in SnackbarLiteral.Matches(text))
            AddHardcoded(result, sourceRoot, file, line, m.Groups["value"].Value, "Snackbar", byKey);
    }

    private static void AddHardcoded(
        LocalizationAuditResultDto result,
        string sourceRoot,
        string file,
        int line,
        string value,
        string component,
        IReadOnlyDictionary<string, List<RawEntry>> byKey)
    {
        value = value.Trim();
        if (!LooksLikeUiText(value)) return;
        var existing = byKey.Values.SelectMany(x => x).FirstOrDefault(x => string.Equals(x.Value, value, StringComparison.Ordinal));
        var suggested = SuggestKey(file, value);
        Add(result, "HardCodedUi", Relative(sourceRoot, file), line, existing?.Key, component, value,
            existing?.Language == "vi" ? existing.Value : null,
            existing?.Language == "ja" ? existing.Value : null,
            existing is null
                ? $"UI đang dùng text cứng '{value}'. Có thể tạo key '{suggested}'."
                : $"UI đang dùng text cứng '{value}'. Đã tìm thấy key '{existing.Key}'.",
            suggested);
    }

    private static void Add(
        LocalizationAuditResultDto result,
        string type,
        string sourceFile,
        int? line,
        string? existingKey,
        string? component,
        string? detectedText,
        string? vi,
        string? ja,
        string detail,
        string? suggestedKey = null)
    {
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{type}|{sourceFile}|{line}|{existingKey}|{detectedText}|{detail}")))[..16];

        result.Findings.Add(new LocalizationAuditFindingDto
        {
            Id = id,
            Type = type,
            SourceFile = sourceFile,
            Line = line,
            Component = component,
            ExistingKey = existingKey,
            DetectedText = detectedText,
            SuggestedKey = suggestedKey,
            ViValue = vi,
            JaValue = ja,
            Detail = detail
        });
    }

    private LocalizationCatalogDto ReadCatalog()
    {
        var raw = ReadRawCatalog(ResolveLocalizationRoot());
        var entries = raw.GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new LocalizationEntryDto
            {
                Key = g.Key,
                Module = g.First().Module,
                Vi = g.FirstOrDefault(x => x.Language == "vi")?.Value ?? string.Empty,
                Ja = g.FirstOrDefault(x => x.Language == "ja")?.Value ?? string.Empty
            })
            .OrderBy(x => x.Module).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new LocalizationCatalogDto
        {
            Entries = entries,
            Modules = entries.Select(x => x.Module).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
            ViCount = entries.Count(x => !string.IsNullOrWhiteSpace(x.Vi)),
            JaCount = entries.Count(x => !string.IsNullOrWhiteSpace(x.Ja)),
            GeneratedAt = DateTime.Now
        };
    }

    private static List<RawEntry> ReadRawCatalog(string root)
    {
        var list = new List<RawEntry>();
        if (!Directory.Exists(root)) return list;

        foreach (var file in Directory.EnumerateFiles(root, "vi.*.json", SearchOption.TopDirectoryOnly))
            ReadLanguageFile(file, "vi", list);
        foreach (var file in Directory.EnumerateFiles(root, "ja.*.json", SearchOption.TopDirectoryOnly))
            ReadLanguageFile(file, "ja", list);

        return list;
    }

    private static void ReadLanguageFile(string file, string language, List<RawEntry> target)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        var module = name[(language.Length + 1)..];
        var json = File.ReadAllText(file, Encoding.UTF8);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"'{Path.GetFileName(file)}' phải là JSON object.");

        foreach (var property in doc.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException($"'{Path.GetFileName(file)}': key '{property.Name}' phải có value dạng string.");
            target.Add(new RawEntry(property.Name, module, language, property.Value.GetString() ?? string.Empty));
        }
    }

    private void WriteEntry(string root, string module, string key, string vi, string ja)
    {
        Directory.CreateDirectory(root);
        WriteLanguageEntry(root, "vi", module, key, vi);
        WriteLanguageEntry(root, "ja", module, key, ja);
    }

    private static void WriteLanguageEntry(string root, string language, string module, string key, string value)
    {
        var file = Path.Combine(root, $"{language}.{module}.json");
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(file))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file, Encoding.UTF8));
            foreach (var p in doc.RootElement.EnumerateObject())
                dict[p.Name] = p.Value.GetString() ?? string.Empty;
        }
        dict[key] = value;
        var ordered = dict.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        File.WriteAllText(file, JsonSerializer.Serialize(ordered, JsonOptions) + Environment.NewLine, new UTF8Encoding(false));
    }

    private static void RemoveKey(string root, string module, string key)
    {
        foreach (var language in new[] { "vi", "ja" })
        {
            var file = Path.Combine(root, $"{language}.{module}.json");
            if (!File.Exists(file)) continue;
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using var doc = JsonDocument.Parse(File.ReadAllText(file, Encoding.UTF8));
            foreach (var p in doc.RootElement.EnumerateObject())
                if (!string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase))
                    dict[p.Name] = p.Value.GetString() ?? string.Empty;
            File.WriteAllText(file, JsonSerializer.Serialize(dict.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value), JsonOptions) + Environment.NewLine, new UTF8Encoding(false));
        }
    }

    private string RequireLocalizationRoot()
    {
        var root = ResolveLocalizationRoot();
        if (!Directory.Exists(root))
            throw new InvalidOperationException($"Không tìm thấy thư mục JSON ngôn ngữ: '{root}'. Hãy cấu hình Localization:SourceRoot trỏ tới thư mục FVN_REGISTER.Shared/Localization.");
        return root;
    }

    private string ResolveLocalizationRoot()
    {
        var configured = _configuration["Localization:SourceRoot"];
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(configured);

        var shared = ResolveSharedRoot();
        var root = Path.Combine(shared, "Localization");
        if (Directory.Exists(root)) return root;

        return Path.Combine(_environment.ContentRootPath, "Localization");
    }

    private string ResolveSharedRoot()
    {
        var configured = _configuration["Localization:SharedRoot"];
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(configured);

        return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", "FVN_REGISTER.Shared"));
    }

    private static string Relative(string root, string file) =>
        Path.GetRelativePath(root, file).Replace('\\', '/');

    private static bool IsExcludedSource(string file)
    {
        var normalized = file.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/Localization/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/Services/Language/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeUiText(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 2) return false;
        if (value.StartsWith("@") || value.Contains("://") || value.StartsWith("/") || value.StartsWith("{") || value.StartsWith("["))
            return false;
        if (Regex.IsMatch(value, @"^[A-Za-z0-9_./:%+\-–—→•()\[\]{}]+$"))
            return false;
        return Regex.IsMatch(value, @"[A-Za-zÀ-ỹぁ-んァ-ヶ一-龯]");
    }

    private static string SuggestKey(string file, string value)
    {
        var stem = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
        var ascii = Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", ".");
        ascii = ascii.Trim('.');
        if (ascii.Length > 50) ascii = ascii[..50].Trim('.');
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..6].ToLowerInvariant();
        return string.IsNullOrWhiteSpace(ascii) ? $"ui.{stem}.{hash}" : $"ui.{stem}.{ascii}.{hash}";
    }

    private static string InferModule(string key)
    {
        var dot = key.IndexOf('.');
        return dot > 0 ? key[..dot] : "common";
    }

    private static void ValidateEntry(string key, string module, string vi, string ja)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Key không được để trống.");
        if (!Regex.IsMatch(key.Trim(), @"^[A-Za-z][A-Za-z0-9_.-]{1,149}$"))
            throw new InvalidOperationException($"Key '{key}' không hợp lệ.");
        if (string.IsNullOrWhiteSpace(module))
            throw new InvalidOperationException("Module không được để trống.");
        if (!Regex.IsMatch(module.Trim(), @"^[A-Za-z][A-Za-z0-9_-]{0,49}$"))
            throw new InvalidOperationException($"Module '{module}' không hợp lệ.");
        if (string.IsNullOrWhiteSpace(vi) || string.IsNullOrWhiteSpace(ja))
            throw new InvalidOperationException("VI và JA không được để trống.");
    }

    private static void ValidatePlaceholders(string vi, string ja)
    {
        if (!PlaceholderSet(vi).SequenceEqual(PlaceholderSet(ja)))
            throw new InvalidOperationException("VI và JA phải có cùng tập placeholder {0}, {1}... .");
    }

    private static string[] PlaceholderSet(string? value) =>
        Placeholder.Matches(value ?? string.Empty).Select(x => x.Groups[1].Value).OrderBy(x => x).ToArray();

    private sealed record RawEntry(string Key, string Module, string Language, string Value);
}
