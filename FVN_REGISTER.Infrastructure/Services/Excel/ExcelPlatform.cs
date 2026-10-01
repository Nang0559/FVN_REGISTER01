using FVN_REGISTER.Application.Interfaces.Excel;
using FVN_REGISTER.Core.Excel;
using Microsoft.EntityFrameworkCore;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Data;
using System.Text.Json;

namespace FVN_REGISTER.Infrastructure.Services.Excel;

public sealed class ExcelPlatform : IExcelPlatform
{
    private readonly DbContext _db;
    public ExcelPlatform(DbContext db) => _db = db;

    public Task<ExcelWorkbookInspection> InspectAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        using var wb = Open(content, fileName);
        var sheets = new List<ExcelSheetInspection>();
        for (var i = 0; i < wb.NumberOfSheets; i++)
        {
            var sheet = wb.GetSheetAt(i);
            var first = sheet.FirstRowNum;
            var last = sheet.LastRowNum;
            var min = int.MaxValue;
            var max = -1;
            for (var r = first; r <= last; r++)
            {
                var row = sheet.GetRow(r);
                if (row is null) continue;
                min = Math.Min(min, row.FirstCellNum < 0 ? 0 : row.FirstCellNum);
                max = Math.Max(max, row.LastCellNum - 1);
            }
            sheets.Add(new ExcelSheetInspection(i, sheet.SheetName, first, last, min == int.MaxValue ? 0 : min, max,
                FindHeaders(sheet, first, Math.Min(last, first + 30), max)));
        }
        return Task.FromResult(new ExcelWorkbookInspection(fileName, Path.GetExtension(fileName).ToLowerInvariant(), content.CanSeek ? content.Length : 0, sheets));
    }

    public Task<ExcelPreviewResult> PreviewAsync(Stream content, string fileName, ExcelSchemaDefinition schema, CancellationToken ct = default)
        => ReadAsync(content, fileName, schema, ct);

    public async Task<ExcelImportResult> ImportAsync(Stream content, string fileName, ExcelSchemaDefinition schema, CancellationToken ct = default)
    {
        var preview = await ReadAsync(content, fileName, schema, ct);
        return new ExcelImportResult(preview.Rows, preview.Errors, preview.TotalRows, preview.ValidRows, preview.InvalidRows);
    }

    public Task<Stream> ExportAsync(string fileName, ExcelSchemaDefinition schema, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows, CancellationToken ct = default)
    {
        IWorkbook workbook = fileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase) ? new HSSFWorkbook() : new XSSFWorkbook();
        var sheet = workbook.CreateSheet(schema.EntityCode);
        var header = sheet.CreateRow(0);
        for (var c = 0; c < schema.Fields.Count; c++)
            header.CreateCell(c).SetCellValue(schema.Fields[c].HeaderName ?? schema.Fields[c].FieldKey);
        for (var r = 0; r < rows.Count; r++)
        {
            var row = sheet.CreateRow(r + 1);
            for (var c = 0; c < schema.Fields.Count; c++)
            {
                rows[r].TryGetValue(schema.Fields[c].FieldKey, out var value);
                row.CreateCell(c).SetCellValue(value?.ToString() ?? string.Empty);
            }
        }
        using var output = new MemoryStream();
        workbook.Write(output, true);
        workbook.Close();
        return Task.FromResult<Stream>(new MemoryStream(output.ToArray()));
    }

    public async Task<IReadOnlyList<ExcelSchemaSummary>> GetSchemasAsync(string moduleCode, string entityCode, bool includeRetired = false, CancellationToken ct = default)
    {
        var sql = $"""
            SELECT s.Id, s.ModuleCode, s.EntityCode, s.SchemaKey, s.SchemaName,
                   v.VersionNo, s.Status, s.SourceFileName, s.CreatedAt, s.UpdatedAt
            FROM dbo.F03ExcelSchemas s
            LEFT JOIN dbo.F03ExcelSchemaVersions v ON v.Id = s.CurrentVersionId
            WHERE s.ModuleCode=@m AND s.EntityCode=@e
              AND (@includeRetired=1 OR s.Status<>2)
            ORDER BY s.SchemaKey, v.VersionNo DESC
            """;
        await using var cmd = Command(sql, ("@m", moduleCode), ("@e", entityCode), ("@includeRetired", includeRetired ? 1 : 0));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<ExcelSchemaSummary>();
        while (await reader.ReadAsync(ct)) list.Add(ReadSummary(reader));
        return list;
    }

    public async Task<ExcelSchemaSummary?> GetSchemaAsync(int schemaId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT s.Id, s.ModuleCode, s.EntityCode, s.SchemaKey, s.SchemaName,
                   v.VersionNo, s.Status, s.SourceFileName, s.CreatedAt, s.UpdatedAt
            FROM dbo.F03ExcelSchemas s
            LEFT JOIN dbo.F03ExcelSchemaVersions v ON v.Id=s.CurrentVersionId
            WHERE s.Id=@id;
            """;
        await using var cmd = Command(sql, ("@id", schemaId));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadSummary(reader) : null;
    }

    public async Task<ExcelSchemaDefinition?> GetSchemaDefinitionAsync(int schemaId, CancellationToken ct = default)
    {
        const string schemaSql = """
            SELECT s.ModuleCode, s.EntityCode, s.SchemaKey, v.VersionNo,
                   v.SheetIndex, v.SheetName, v.HeaderRowIndex, v.DataStartRowIndex,
                   v.DataEndRowIndex, v.SelectedColumnsJson, v.Culture
            FROM dbo.F03ExcelSchemas s
            INNER JOIN dbo.F03ExcelSchemaVersions v ON v.Id=s.CurrentVersionId
            WHERE s.Id=@id AND s.Status<>920;
            """;
        await using var schemaCommand = Command(schemaSql, ("@id", schemaId));
        await using var reader = await schemaCommand.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        var moduleCode = reader.GetString(0);
        var entityCode = reader.GetString(1);
        var schemaKey = reader.GetString(2);
        var version = reader.GetInt32(3);
        var sheetIndex = reader.GetInt32(4);
        var sheetName = reader.GetString(5);
        var headerRow = reader.GetInt32(6);
        var dataStart = reader.GetInt32(7);
        var dataEnd = reader.IsDBNull(8) ? null : reader.GetInt32(8);
        var selectedColumns = JsonSerializer.Deserialize<IReadOnlyList<int>>(reader.GetString(9)) ?? Array.Empty<int>();
        var culture = reader.IsDBNull(10) ? "vi-VN" : reader.GetString(10);
        await reader.CloseAsync();

        const string fieldSql = """
            SELECT FieldKey, DataType, IsRequired, SourceColumnIndex, HeaderName,
                   Format, ResourceKey, TargetProperty, DisplayOrder, AllowEmpty
            FROM dbo.F03ExcelSchemaFields
            WHERE SchemaVersionId=(SELECT CurrentVersionId FROM dbo.F03ExcelSchemas WHERE Id=@id)
            ORDER BY DisplayOrder, Id;
            """;
        await using var fieldCommand = Command(fieldSql, ("@id", schemaId));
        await using var fieldReader = await fieldCommand.ExecuteReaderAsync(ct);
        var fields = new List<ExcelSchemaField>();
        while (await fieldReader.ReadAsync(ct))
        {
            fields.Add(new ExcelSchemaField(
                fieldReader.GetString(0), fieldReader.GetString(1), fieldReader.GetBoolean(2),
                fieldReader.GetInt32(3), fieldReader.IsDBNull(4) ? null : fieldReader.GetString(4),
                fieldReader.IsDBNull(5) ? null : fieldReader.GetString(5),
                fieldReader.IsDBNull(6) ? null : fieldReader.GetString(6),
                fieldReader.IsDBNull(7) ? null : fieldReader.GetString(7),
                fieldReader.GetInt32(8), fieldReader.GetBoolean(9)));
        }
        return new ExcelSchemaDefinition(moduleCode, entityCode, schemaKey, version, sheetIndex, sheetName,
            headerRow, dataStart, dataEnd, selectedColumns, fields, culture);
    }

    public async Task<ExcelSchemaSummary> CreateDraftSchemaAsync(ExcelSchemaCreateRequest request, string? sourceFileName, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var schemaId = await ExecuteScalarIntAsync("""
                INSERT INTO dbo.F03ExcelSchemas(ModuleCode,EntityCode,SchemaKey,SchemaName,Description,Status,SourceFileName,IsActive,CreatedAt,UpdatedAt)
                VALUES(@m,@e,@k,@n,NULL,0,@f,0,SYSUTCDATETIME(),SYSUTCDATETIME());
                SELECT CAST(SCOPE_IDENTITY() AS int);
                """, ("@m", request.ModuleCode), ("@e", request.EntityCode), ("@k", request.SchemaKey),
                ("@n", request.SchemaName), ("@f", sourceFileName ?? (object)DBNull.Value));

            var versionId = await ExecuteScalarIntAsync("""
                INSERT INTO dbo.F03ExcelSchemaVersions(SchemaId,VersionNo,Status,SheetIndex,SheetName,HeaderRowIndex,DataStartRowIndex,DataEndRowIndex,SelectedColumnsJson,Culture,CreatedAt)
                VALUES(@id,1,0,@si,@sn,@hr,@ds,@de,@cols,@culture,SYSUTCDATETIME());
                SELECT CAST(SCOPE_IDENTITY() AS int);
                """, ("@id", schemaId), ("@si", request.SheetIndex), ("@sn", request.SheetName),
                ("@hr", request.HeaderRowIndex), ("@ds", request.DataStartRowIndex), ("@de", request.DataEndRowIndex ?? (object)DBNull.Value),
                ("@cols", JsonSerializer.Serialize(request.SelectedColumnIndexes)), ("@culture", request.Culture));

            foreach (var field in request.Fields)
            {
                await ExecuteNonQueryAsync("""
                    INSERT INTO dbo.F03ExcelSchemaFields(SchemaVersionId,FieldKey,DataType,SourceColumnIndex,SourceColumnName,HeaderName,ResourceKey,TargetProperty,Format,ValidationRule,DefaultValue,IsRequired,AllowEmpty,DisplayOrder)
                    VALUES(@v,@key,@type,@col,NULL,@header,@resource,@target,@format,NULL,NULL,@required,@allow,@order);
                    """, ("@v", versionId), ("@key", field.FieldKey), ("@type", field.DataType), ("@col", field.SourceColumnIndex),
                    ("@header", field.HeaderName ?? (object)DBNull.Value), ("@resource", field.ResourceKey ?? (object)DBNull.Value),
                    ("@target", field.TargetProperty ?? (object)DBNull.Value), ("@format", field.Format ?? (object)DBNull.Value),
                    ("@required", field.Required), ("@allow", field.AllowEmpty), ("@order", field.DisplayOrder));
            }
            await ExecuteNonQueryAsync("UPDATE dbo.F03ExcelSchemas SET CurrentVersionId=@v, UpdatedAt=SYSUTCDATETIME() WHERE Id=@id;", ("@v", versionId), ("@id", schemaId));
            await transaction.CommitAsync(ct);
            return (await GetSchemaAsync(schemaId, ct))!;
        }
        catch { await transaction.RollbackAsync(ct); throw; }
    }

    public async Task<ExcelSchemaSummary> ActivateSchemaAsync(int schemaId, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var key = await ExecuteSingleAsync("SELECT ModuleCode + CHAR(31) + EntityCode + CHAR(31) + SchemaKey FROM dbo.F03ExcelSchemas WHERE Id=@id", ("@id", schemaId));
            if (key is null) throw new InvalidOperationException("Schema not found.");
            var parts = key.Split((char)31);
            await ExecuteNonQueryAsync("UPDATE dbo.F03ExcelSchemas SET Status=2,IsActive=0,UpdatedAt=SYSUTCDATETIME() WHERE ModuleCode=@m AND EntityCode=@e AND SchemaKey=@k AND Id<>@id AND Status=1;", ("@m", parts[0]), ("@e", parts[1]), ("@k", parts[2]), ("@id", schemaId));
            await ExecuteNonQueryAsync("UPDATE dbo.F03ExcelSchemas SET Status=1,IsActive=1,UpdatedAt=SYSUTCDATETIME() WHERE Id=@id AND Status=0;", ("@id", schemaId));
            await transaction.CommitAsync(ct);
            return (await GetSchemaAsync(schemaId, ct)) ?? throw new InvalidOperationException("Schema cannot be activated.");
        }
        catch { await transaction.RollbackAsync(ct); throw; }
    }

    public async Task DeleteDraftSchemaAsync(int schemaId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dbo.F03ExcelSchemas
            SET Status=920, IsActive=0, UpdatedAt=SYSUTCDATETIME()
            WHERE Id=@id AND Status=0
              AND NOT EXISTS (SELECT 1 FROM dbo.F03ExcelImportBatches WHERE SchemaId=@id AND Status IN (40,100));
            """;
        if (await ExecuteNonQueryAsync(sql, ("@id", schemaId)) == 0)
            throw new InvalidOperationException("Only an unused Draft schema can be deleted.");
    }

    private async Task<ExcelPreviewResult> ReadAsync(Stream content, string fileName, ExcelSchemaDefinition schema, CancellationToken ct)
    {
        using var wb = Open(content, fileName);
        if (schema.SheetIndex < 0 || schema.SheetIndex >= wb.NumberOfSheets) throw new InvalidOperationException("Schema sheet does not exist.");
        var sheet = wb.GetSheetAt(schema.SheetIndex);
        var rows = new List<ExcelRow>();
        var errors = new List<ExcelValidationError>();
        var end = Math.Min(schema.DataEndRowIndex ?? sheet.LastRowNum, sheet.LastRowNum);
        for (var r = Math.Max(0, schema.DataStartRowIndex); r <= end; r++)
        {
            ct.ThrowIfCancellationRequested();
            var source = sheet.GetRow(r);
            if (source is null) continue;
            var cells = new Dictionary<int, string?>();
            foreach (var field in schema.Fields)
            {
                var value = source.GetCell(field.SourceColumnIndex)?.ToString();
                cells[field.SourceColumnIndex] = value;
                if (field.Required && string.IsNullOrWhiteSpace(value))
                    errors.Add(new ExcelValidationError(r + 1, field.SourceColumnIndex, field.FieldKey, "REQUIRED", $"{field.FieldKey} is required", value));
            }
            if (cells.Values.All(string.IsNullOrWhiteSpace)) continue;
            rows.Add(new ExcelRow(r + 1, cells));
        }
        var invalid = errors.Select(e => e.RowNumber).Distinct().Count();
        return new ExcelPreviewResult(rows, errors, rows.Count, rows.Count - invalid, invalid);
    }

    private static IReadOnlyList<ExcelHeaderCandidate> FindHeaders(ISheet sheet, int first, int last, int max)
    {
        var list = new List<ExcelHeaderCandidate>();
        for (var r = first; r <= last; r++)
        {
            var row = sheet.GetRow(r);
            if (row is null) continue;
            var values = new List<string?>(); var nonEmpty = 0;
            for (var c = 0; c <= Math.Max(max, 0); c++) { var value = row.GetCell(c)?.ToString(); values.Add(value); if (!string.IsNullOrWhiteSpace(value)) nonEmpty++; }
            if (nonEmpty > 0) list.Add(new ExcelHeaderCandidate(r, values, values.Count == 0 ? 0 : nonEmpty / (double)values.Count));
        }
        return list;
    }

    private static IWorkbook Open(Stream content, string fileName) =>
        fileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase) ? new HSSFWorkbook(content) :
        fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ? new XSSFWorkbook(content) :
        throw new InvalidOperationException("Only .xls and .xlsx files are supported.");

    private DbCommand Command(string sql, params (string Name, object Value)[] parameters)
    {
        var command = _db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        foreach (var parameter in parameters) { var p = command.CreateParameter(); p.ParameterName = parameter.Name; p.Value = parameter.Value ?? DBNull.Value; command.Parameters.Add(p); }
        if (command.Connection!.State != ConnectionState.Open) command.Connection.Open();
        return command;
    }

    private async Task<int> ExecuteNonQueryAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = Command(sql, parameters);
        return await command.ExecuteNonQueryAsync();
    }

    private async Task<int> ExecuteScalarIntAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = Command(sql, parameters);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task<string?> ExecuteSingleAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = Command(sql, parameters);
        var value = await command.ExecuteScalarAsync();
        return value == null || value == DBNull.Value ? null : value.ToString();
    }

    private static ExcelSchemaSummary ReadSummary(System.Data.Common.DbDataReader reader) =>
        new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
            reader.IsDBNull(5) ? 0 : reader.GetInt32(5), (ExcelSchemaStatus)reader.GetInt32(6),
            reader.IsDBNull(7) ? null : reader.GetString(7), reader.GetDateTime(8), reader.IsDBNull(9) ? null : reader.GetDateTime(9));
}
