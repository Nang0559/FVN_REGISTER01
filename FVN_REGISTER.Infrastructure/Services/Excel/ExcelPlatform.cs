using FVN_REGISTER.Application.Interfaces.Excel;
using FVN_REGISTER.Core.Excel;
using Microsoft.EntityFrameworkCore;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Data;
using System.Data.Common;
using System.Text.Json;

namespace FVN_REGISTER.Infrastructure.Services.Excel;

public sealed class ExcelPlatform : IExcelPlatform
{
    private readonly DbContext _db;
    public ExcelPlatform(DbContext db) => _db = db;

    public Task<ExcelWorkbookInspection> InspectAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        using var workbook = Open(content, fileName);
        var sheets = new List<ExcelSheetInspection>();
        for (var i = 0; i < workbook.NumberOfSheets; i++)
        {
            ct.ThrowIfCancellationRequested();
            var sheet = workbook.GetSheetAt(i);
            var first = sheet.FirstRowNum;
            var last = sheet.LastRowNum;
            var min = int.MaxValue;
            var max = -1;
            for (var rowIndex = first; rowIndex <= last; rowIndex++)
            {
                var row = sheet.GetRow(rowIndex);
                if (row is null) continue;
                min = Math.Min(min, row.FirstCellNum < 0 ? 0 : row.FirstCellNum);
                max = Math.Max(max, row.LastCellNum - 1);
            }
            sheets.Add(new ExcelSheetInspection(i, sheet.SheetName, first, last, min == int.MaxValue ? 0 : min, max, FindHeaders(sheet, first, Math.Min(last, first + 30), max)));
        }
        return Task.FromResult(new ExcelWorkbookInspection(Path.GetFileName(fileName), Path.GetExtension(fileName).ToLowerInvariant(), content.CanSeek ? content.Length : 0, sheets));
    }

    public Task<ExcelSheetGrid> ReadGridAsync(Stream content, string fileName, int sheetIndex, int maxRows = 200, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (sheetIndex < 0) throw new ArgumentOutOfRangeException(nameof(sheetIndex), "Sheet Excel không hợp lệ.");
        maxRows = Math.Clamp(maxRows, 1, 10000);

        using var workbook = Open(content, fileName);
        if (sheetIndex >= workbook.NumberOfSheets)
            throw new InvalidOperationException("Sheet Excel không hợp lệ.");

        var sheet = workbook.GetSheetAt(sheetIndex);
        var lastRow = sheet.LastRowNum;
        var totalRows = sheet.PhysicalNumberOfRows == 0 ? 0 : Math.Max(0, lastRow + 1);
        var columnCount = 0;
        for (var rowIndex = Math.Max(0, sheet.FirstRowNum); rowIndex <= lastRow; rowIndex++)
        {
            ct.ThrowIfCancellationRequested();
            var row = sheet.GetRow(rowIndex);
            if (row is not null && row.LastCellNum > columnCount)
                columnCount = row.LastCellNum;
        }

        var rows = new List<ExcelGridRow>();
        var endRow = Math.Min(lastRow, maxRows - 1);
        for (var rowIndex = 0; rowIndex <= endRow && totalRows > 0; rowIndex++)
        {
            ct.ThrowIfCancellationRequested();
            var source = sheet.GetRow(rowIndex);
            var cells = new string?[columnCount];
            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                var cell = source?.GetCell(columnIndex);
                var value = cell?.ToString();
                cells[columnIndex] = string.IsNullOrWhiteSpace(value) ? null : value;
            }
            rows.Add(new ExcelGridRow(rowIndex, cells));
        }

        return Task.FromResult(new ExcelSheetGrid(sheetIndex, sheet.SheetName, totalRows, columnCount, rows));
    }

    public Task<ExcelPreviewResult> PreviewAsync(Stream content, string fileName, ExcelSchemaDefinition schema, CancellationToken ct = default) => ReadAsync(content, fileName, schema, ct);

    public async Task<ExcelImportResult> ImportAsync(Stream content, string fileName, ExcelSchemaDefinition schema, CancellationToken ct = default)
    {
        var preview = await ReadAsync(content, fileName, schema, ct);
        return new ExcelImportResult(preview.Rows, preview.Errors, preview.TotalRows, preview.ValidRows, preview.InvalidRows);
    }

    public Task<Stream> ExportAsync(string fileName, ExcelSchemaDefinition schema, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        IWorkbook workbook = fileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase) ? new HSSFWorkbook() : new XSSFWorkbook();
        var sheet = workbook.CreateSheet(schema.EntityCode);
        var header = sheet.CreateRow(0);
        for (var column = 0; column < schema.Fields.Count; column++) header.CreateCell(column).SetCellValue(schema.Fields[column].HeaderName ?? schema.Fields[column].FieldKey);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = sheet.CreateRow(rowIndex + 1);
            for (var column = 0; column < schema.Fields.Count; column++) { rows[rowIndex].TryGetValue(schema.Fields[column].FieldKey, out var value); row.CreateCell(column).SetCellValue(value?.ToString() ?? string.Empty); }
        }
        using var output = new MemoryStream();
        workbook.Write(output, true);
        workbook.Close();
        return Task.FromResult<Stream>(new MemoryStream(output.ToArray()));
    }

    public async Task<IReadOnlyList<ExcelSchemaSummary>> GetSchemasAsync(string moduleCode, string entityCode, bool includeRetired = false, CancellationToken ct = default)
    {
        const string sql = """
            SELECT s.Id, s.ModuleCode, s.EntityCode, s.SchemaCode, s.SchemaName,
                   v.VersionNo, s.Status, v.SourceFileName, s.CreatedAt, s.UpdatedAt
            FROM dbo.F03ExcelSchemas s
            LEFT JOIN dbo.F03ExcelSchemaVersions v ON v.Id=s.CurrentVersionId
            WHERE s.ModuleCode=@m AND s.EntityCode=@e AND s.Status<>920
              AND (@includeRetired=1 OR s.Status<>2)
            ORDER BY s.SchemaCode, v.VersionNo DESC;
            """;
        await using var command = Command(sql, ("@m", moduleCode), ("@e", entityCode), ("@includeRetired", includeRetired ? 1 : 0));
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<ExcelSchemaSummary>();
        while (await reader.ReadAsync(ct)) result.Add(ReadSummary(reader));
        return result;
    }

    public async Task<ExcelSchemaSummary?> GetSchemaAsync(int schemaId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT s.Id, s.ModuleCode, s.EntityCode, s.SchemaCode, s.SchemaName,
                   v.VersionNo, s.Status, v.SourceFileName, s.CreatedAt, s.UpdatedAt
            FROM dbo.F03ExcelSchemas s
            LEFT JOIN dbo.F03ExcelSchemaVersions v ON v.Id=s.CurrentVersionId
            WHERE s.Id=@id AND s.Status<>920;
            """;
        await using var command = Command(sql, ("@id", schemaId));
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadSummary(reader) : null;
    }

    public async Task<ExcelSchemaDefinition?> GetSchemaDefinitionAsync(int schemaId, CancellationToken ct = default)
    {
        const string schemaSql = """
            SELECT s.ModuleCode, s.EntityCode, s.SchemaCode, v.VersionNo,
                   v.SheetIndex, v.SheetName, v.HeaderRowIndex, v.DataStartRowIndex,
                   v.DataEndRowIndex, v.SelectedColumnsJson, v.Culture
            FROM dbo.F03ExcelSchemas s
            INNER JOIN dbo.F03ExcelSchemaVersions v ON v.Id=s.CurrentVersionId
            WHERE s.Id=@id AND s.Status=1;
            """;
        await using var schemaCommand = Command(schemaSql, ("@id", schemaId));
        await using var schemaReader = await schemaCommand.ExecuteReaderAsync(ct);
        if (!await schemaReader.ReadAsync(ct)) return null;
        var moduleCode = schemaReader.GetString(0);
        var entityCode = schemaReader.GetString(1);
        var schemaCode = schemaReader.GetString(2);
        var version = schemaReader.GetInt32(3);
        var sheetIndex = schemaReader.GetInt32(4);
        var sheetName = schemaReader.GetString(5);
        var headerRow = schemaReader.GetInt32(6);
        var dataStart = schemaReader.GetInt32(7);
        int? dataEnd = schemaReader.IsDBNull(8) ? null : schemaReader.GetInt32(8);
        var selectedColumns = JsonSerializer.Deserialize<IReadOnlyList<int>>(schemaReader.GetString(9)) ?? Array.Empty<int>();
        var culture = schemaReader.IsDBNull(10) ? "vi-VN" : schemaReader.GetString(10);
        await schemaReader.CloseAsync();

        const string fieldSql = """
            SELECT FieldKey, DataType, IsRequired, SourceColumnIndex, HeaderName,
                   Format, ResourceKey, TargetProperty, DisplayOrder, AllowEmpty,
                   MaxLength, DefaultValue, ValidationRule
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
                fieldReader.GetString(0), fieldReader.GetString(1), fieldReader.GetBoolean(2), fieldReader.GetInt32(3),
                fieldReader.IsDBNull(4) ? null : fieldReader.GetString(4), fieldReader.IsDBNull(5) ? null : fieldReader.GetString(5),
                fieldReader.IsDBNull(6) ? null : fieldReader.GetString(6), fieldReader.IsDBNull(7) ? null : fieldReader.GetString(7),
                fieldReader.GetInt32(8), fieldReader.GetBoolean(9), fieldReader.IsDBNull(10) ? null : fieldReader.GetInt32(10),
                fieldReader.IsDBNull(11) ? null : fieldReader.GetString(11), fieldReader.IsDBNull(12) ? null : fieldReader.GetString(12)));
        }
        return new ExcelSchemaDefinition(moduleCode, entityCode, schemaCode, version, sheetIndex, sheetName, headerRow, dataStart, dataEnd, selectedColumns, fields, culture);
    }

    public async Task<ExcelSchemaSummary> CreateDraftSchemaAsync(ExcelSchemaCreateRequest request, string? sourceFileName, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var schemaId = await ExecuteScalarIntAsync("""
                INSERT INTO dbo.F03ExcelSchemas(ModuleCode,EntityCode,SchemaCode,SchemaName,Description,Status,IsActive,CreatedAt,UpdatedAt)
                VALUES(@m,@e,@k,@n,NULL,0,1,SYSUTCDATETIME(),SYSUTCDATETIME());
                SELECT CAST(SCOPE_IDENTITY() AS int);
                """, ("@m", request.ModuleCode), ("@e", request.EntityCode), ("@k", request.SchemaKey), ("@n", request.SchemaName));
            var versionId = await ExecuteScalarIntAsync("""
                INSERT INTO dbo.F03ExcelSchemaVersions(SchemaId,VersionNo,Status,SheetIndex,SheetName,HeaderRowIndex,DataStartRowIndex,DataEndRowIndex,SelectedColumnsJson,Culture,SourceFileName,CreatedAt)
                VALUES(@id,1,0,@si,@sn,@hr,@ds,@de,@cols,@culture,@file,SYSUTCDATETIME());
                SELECT CAST(SCOPE_IDENTITY() AS int);
                """, ("@id", schemaId), ("@si", request.SheetIndex), ("@sn", request.SheetName), ("@hr", request.HeaderRowIndex),
                ("@ds", request.DataStartRowIndex), ("@de", request.DataEndRowIndex ?? (object)DBNull.Value),
                ("@cols", JsonSerializer.Serialize(request.SelectedColumnIndexes)), ("@culture", request.Culture), ("@file", sourceFileName ?? (object)DBNull.Value));
            foreach (var field in request.Fields)
            {
                await ExecuteNonQueryAsync("""
                    INSERT INTO dbo.F03ExcelSchemaFields
                    (SchemaVersionId,FieldKey,DataType,SourceColumnIndex,HeaderName,ResourceKey,TargetProperty,Format,ValidationRule,DefaultValue,IsRequired,AllowEmpty,MaxLength,DisplayOrder)
                    VALUES(@v,@key,@type,@col,@header,@resource,@target,@format,@rule,@defaultValue,@required,@allow,@maxLength,@order);
                    """, ("@v", versionId), ("@key", field.FieldKey), ("@type", field.DataType), ("@col", field.SourceColumnIndex),
                    ("@header", field.HeaderName ?? (object)DBNull.Value), ("@resource", field.ResourceKey ?? (object)DBNull.Value), ("@target", field.TargetProperty ?? (object)DBNull.Value),
                    ("@format", field.Format ?? (object)DBNull.Value), ("@rule", field.ValidationRule ?? (object)DBNull.Value), ("@defaultValue", field.DefaultValue ?? (object)DBNull.Value),
                    ("@required", field.Required), ("@allow", field.AllowEmpty), ("@maxLength", field.MaxLength ?? (object)DBNull.Value), ("@order", field.DisplayOrder));
            }
            await ExecuteNonQueryAsync("UPDATE dbo.F03ExcelSchemas SET CurrentVersionId=@v,UpdatedAt=SYSUTCDATETIME() WHERE Id=@id;", ("@v", versionId), ("@id", schemaId));
            await transaction.CommitAsync(ct);
            return (await GetSchemaAsync(schemaId, ct)) ?? throw new InvalidOperationException("Created schema could not be reloaded.");
        }
        catch { await transaction.RollbackAsync(ct); throw; }
    }

    public async Task<ExcelSchemaSummary> ActivateSchemaAsync(int schemaId, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var key = await ExecuteSingleAsync("SELECT ModuleCode + CHAR(31) + EntityCode + CHAR(31) + SchemaCode FROM dbo.F03ExcelSchemas WHERE Id=@id AND Status=0 AND CurrentVersionId IS NOT NULL;", ("@id", schemaId));
            if (key is null) throw new InvalidOperationException("Only a Draft schema with a version can be activated.");
            var parts = key.Split((char)31);
            await ExecuteNonQueryAsync("UPDATE dbo.F03ExcelSchemas SET Status=2,IsActive=0,UpdatedAt=SYSUTCDATETIME() WHERE ModuleCode=@m AND EntityCode=@e AND SchemaCode=@k AND Id<>@id AND Status=1;", ("@m", parts[0]), ("@e", parts[1]), ("@k", parts[2]), ("@id", schemaId));
            if (await ExecuteNonQueryAsync("UPDATE dbo.F03ExcelSchemas SET Status=1,IsActive=1,UpdatedAt=SYSUTCDATETIME() WHERE Id=@id AND Status=0;", ("@id", schemaId)) == 0) throw new InvalidOperationException("Schema cannot be activated.");
            await ExecuteNonQueryAsync("UPDATE dbo.F03ExcelSchemaVersions SET Status=2 WHERE SchemaId=@id AND Id<> (SELECT CurrentVersionId FROM dbo.F03ExcelSchemas WHERE Id=@id) AND Status=1;", ("@id", schemaId));
            await ExecuteNonQueryAsync("UPDATE dbo.F03ExcelSchemaVersions SET Status=1 WHERE Id=(SELECT CurrentVersionId FROM dbo.F03ExcelSchemas WHERE Id=@id);", ("@id", schemaId));
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
        if (await ExecuteNonQueryAsync(sql, ("@id", schemaId)) == 0) throw new InvalidOperationException("Only an unused Draft schema can be deleted.");
    }

    private static Task<ExcelPreviewResult> ReadAsync(Stream content, string fileName, ExcelSchemaDefinition schema, CancellationToken ct)
    {
        using var workbook = Open(content, fileName);
        if (schema.SheetIndex < 0 || schema.SheetIndex >= workbook.NumberOfSheets) throw new InvalidOperationException("Schema sheet does not exist.");
        var sheet = workbook.GetSheetAt(schema.SheetIndex);
        var rows = new List<ExcelRow>();
        var errors = new List<ExcelValidationError>();
        var end = Math.Min(schema.DataEndRowIndex ?? sheet.LastRowNum, sheet.LastRowNum);
        for (var rowIndex = Math.Max(0, schema.DataStartRowIndex); rowIndex <= end; rowIndex++)
        {
            ct.ThrowIfCancellationRequested();
            var source = sheet.GetRow(rowIndex);
            if (source is null) continue;
            var cells = new Dictionary<int, string?>();
            foreach (var field in schema.Fields)
            {
                var value = source.GetCell(field.SourceColumnIndex)?.ToString();
                cells[field.SourceColumnIndex] = value;
                if (field.Required && string.IsNullOrWhiteSpace(value)) errors.Add(new ExcelValidationError(rowIndex + 1, field.SourceColumnIndex, field.FieldKey, "REQUIRED", $"{field.FieldKey} is required", value));
            }
            if (cells.Values.All(string.IsNullOrWhiteSpace)) continue;
            rows.Add(new ExcelRow(rowIndex + 1, cells));
        }
        var invalidRows = errors.Select(x => x.RowNumber).Distinct().Count();
        return Task.FromResult(new ExcelPreviewResult(rows, errors, rows.Count, rows.Count - invalidRows, invalidRows));
    }

    private static IReadOnlyList<ExcelHeaderCandidate> FindHeaders(ISheet sheet, int first, int last, int max)
    {
        var result = new List<ExcelHeaderCandidate>();
        for (var rowIndex = first; rowIndex <= last; rowIndex++)
        {
            var row = sheet.GetRow(rowIndex);
            if (row is null) continue;
            var values = new List<string?>(); var nonEmpty = 0;
            for (var column = 0; column <= Math.Max(max, 0); column++) { var value = row.GetCell(column)?.ToString(); values.Add(value); if (!string.IsNullOrWhiteSpace(value)) nonEmpty++; }
            if (nonEmpty > 0) result.Add(new ExcelHeaderCandidate(rowIndex, values, values.Count == 0 ? 0 : nonEmpty / (double)values.Count));
        }
        return result;
    }

    private static IWorkbook Open(Stream content, string fileName) =>
        fileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase) ? new HSSFWorkbook(content) :
        fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ? new XSSFWorkbook(content) :
        throw new InvalidOperationException("Only .xls and .xlsx files are supported.");

    private DbCommand Command(string sql, params (string Name, object Value)[] parameters)
    {
        var command = _db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters) { var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value ?? DBNull.Value; command.Parameters.Add(parameter); }
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
        return value is null || value == DBNull.Value ? null : value.ToString();
    }

    private static ExcelSchemaSummary ReadSummary(DbDataReader reader) => new(
        reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
        reader.IsDBNull(5) ? 0 : reader.GetInt32(5), (ExcelSchemaStatus)reader.GetInt32(6),
        reader.IsDBNull(7) ? null : reader.GetString(7), reader.GetDateTime(8), reader.IsDBNull(9) ? null : reader.GetDateTime(9));
}
