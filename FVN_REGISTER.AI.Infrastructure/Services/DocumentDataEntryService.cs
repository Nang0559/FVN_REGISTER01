using System.Globalization;
using System.Text.Json;
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using FVN_REGISTER.AI.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.AI.Infrastructure.Services;

public sealed class DocumentDataEntryService(
    AiPlatformDbContext db,
    IOriginalDocumentStore store,
    IUploadContentValidator uploadValidator,
    IDocumentIngestionService ingestion,
    IDocumentTextExtractor textExtractor,
    IAIModelProvider modelProvider,
    IAIVisionModelProvider visionProvider,
    IFeedbackService feedback,
    XlsxTemplateWriter xlsxWriter) : IDocumentDataEntryService, IDocumentDataEntryTemplateService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    public async Task<DataEntryTemplateView> CreateAsync(string tenantCode, string actorId, string templateCode, string displayName,
        string moduleCode, string schemaCode, Stream templateContent, DataEntryTemplateDefinition definition, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantCode, actorId); ValidateCode(templateCode, 128, nameof(templateCode)); ValidateCode(moduleCode, 64, nameof(moduleCode)); ValidateCode(schemaCode, 128, nameof(schemaCode));
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 256) throw new ArgumentException("Template display name must contain 1 to 256 characters.", nameof(displayName));
        ValidateDefinition(definition);
        var upload = await uploadValidator.ValidateAsync(templateContent, "template.xlsx", cancellationToken);
        if (!upload.MediaType.Equals("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("XLSX_TEMPLATE_REQUIRED");
        var id = Guid.NewGuid();
        var stored = await store.SaveAsync(id, templateContent, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var currentVersion = await db.DataEntryTemplates.Where(x => x.TenantCode == tenantCode && x.TemplateCode == templateCode).Select(x => (int?)x.Version).MaxAsync(cancellationToken) ?? 0;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.DataEntryTemplates.Where(x => x.TenantCode == tenantCode && x.TemplateCode == templateCode && x.IsActive).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), cancellationToken);
        var record = new AiDataEntryTemplateRecord { Id = id, TenantCode = tenantCode, TemplateCode = templateCode, DisplayName = displayName.Trim(), ModuleCode = moduleCode, SchemaCode = schemaCode, Version = currentVersion + 1, StorageKey = stored.StorageKey, WorksheetName = definition.WorksheetName, HeaderRow = definition.HeaderRow, DataStartRow = definition.DataStartRow, DefinitionJson = JsonSerializer.Serialize(definition, JsonOptions), IsActive = true, CreatedBy = actorId, CreatedAtUtc = now, UpdatedAtUtc = now };
        db.DataEntryTemplates.Add(record); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return ToTemplateView(record);
    }

    public async Task<IReadOnlyList<DataEntryTemplateView>> ListAsync(string tenantCode, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantCode, "system");
        var records = await db.DataEntryTemplates.AsNoTracking().Where(x => x.TenantCode == tenantCode && x.IsActive).OrderBy(x => x.TemplateCode).ToListAsync(cancellationToken);
        return records.Select(ToTemplateView).ToArray();
    }

    public async Task<DataEntryRunReceipt> CreateRunAsync(string tenantCode, string actorId, string fileName, Stream documentContent, string templateCode, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantCode, actorId); ValidateCode(templateCode, 128, nameof(templateCode));
        var template = await db.DataEntryTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.TenantCode == tenantCode && x.TemplateCode == templateCode && x.IsActive, cancellationToken) ?? throw new KeyNotFoundException("DATA_ENTRY_TEMPLATE_NOT_FOUND");
        var receipt = await ingestion.IngestAsync(tenantCode, actorId, fileName, documentContent, cancellationToken, template.ModuleCode, template.SchemaCode, DocumentSourceTypes.Upload, $"data-entry:{templateCode}");
        var now = DateTimeOffset.UtcNow;
        var run = new AiDataEntryRunRecord { Id = Guid.NewGuid(), TenantCode = tenantCode, DocumentId = receipt.DocumentId, TemplateId = template.Id, Status = "WaitingForDocument", CreatedBy = actorId, CreatedAtUtc = now, UpdatedAtUtc = now, NextAttemptAtUtc = now };
        db.DataEntryRuns.Add(run); await db.SaveChangesAsync(cancellationToken);
        return new DataEntryRunReceipt(run.Id, run.DocumentId, template.TemplateCode, run.Status, now);
    }

    public async Task<DataEntryRunView?> GetRunAsync(Guid runId, string tenantCode, string actorId, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantCode, actorId);
        return await (from run in db.DataEntryRuns.AsNoTracking()
                      join document in db.Documents.AsNoTracking() on run.DocumentId equals document.Id
                      join template in db.DataEntryTemplates.AsNoTracking() on run.TemplateId equals template.Id
                      where run.Id == runId && run.TenantCode == tenantCode && run.CreatedBy == actorId && document.TenantCode == tenantCode
                      select new DataEntryRunView(run.Id, run.DocumentId, document.FileName, template.TemplateCode, run.Status, run.AttemptCount, run.CandidateRowsJson, run.ValidationJson, run.ConfirmedRowsJson, run.Comment, run.FailureCode, run.ModelProvider, run.ModelVersion, run.CreatedAtUtc, run.CompletedAtUtc, run.UpdatedAtUtc)).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task ProcessAsync(Guid runId, string workerId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var run = await db.DataEntryRuns.SingleOrDefaultAsync(x => x.Id == runId, cancellationToken) ?? throw new KeyNotFoundException("DATA_ENTRY_RUN_NOT_FOUND");
        if (!string.Equals(run.LeaseOwner, workerId, StringComparison.Ordinal) || run.LeaseUntilUtc <= now) throw new InvalidOperationException("DATA_ENTRY_LEASE_LOST");
        if (run.Status is "Confirmed" or "Exported" or "Rejected") return;
        var document = await db.Documents.SingleOrDefaultAsync(x => x.Id == run.DocumentId && x.TenantCode == run.TenantCode, cancellationToken) ?? throw new InvalidOperationException("DATA_ENTRY_DOCUMENT_NOT_FOUND");
        var template = await db.DataEntryTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == run.TemplateId && x.TenantCode == run.TenantCode && x.IsActive, cancellationToken) ?? throw new InvalidOperationException("DATA_ENTRY_TEMPLATE_NOT_FOUND");
        if (document.Status is "Rejected" or "Archived") { run.Status = "Rejected"; run.FailureCode = "SOURCE_DOCUMENT_REJECTED"; run.UpdatedAtUtc = now; await db.SaveChangesAsync(cancellationToken); return; }
        if (document.Status != "AwaitingHumanReview") { run.Status = "WaitingForDocument"; run.NextAttemptAtUtc = now.AddSeconds(15); run.UpdatedAtUtc = now; await db.SaveChangesAsync(cancellationToken); return; }

        var definition = JsonSerializer.Deserialize<DataEntryTemplateDefinition>(template.DefinitionJson, JsonOptions) ?? throw new InvalidDataException("DATA_ENTRY_TEMPLATE_DEFINITION_INVALID");
        ValidateDefinition(definition);
        AIModelResponse response;
        if (document.MediaType is "image/png" or "image/jpeg" && visionProvider is not null && !string.Equals(visionProvider.ModelVersion, "Unconfigured", StringComparison.OrdinalIgnoreCase))
        {
            await using var image = await store.OpenReadAsync(document.StorageKey, cancellationToken);
            response = await visionProvider.AnalyzeImageAsync(image, document.MediaType, BuildRequest(definition, null, "vision-data-entry"), cancellationToken);
        }
        else
        {
            await using var content = await store.OpenReadAsync(document.StorageKey, cancellationToken);
            var text = await textExtractor.ExtractTextAsync(content, document.MediaType, cancellationToken);
            response = await modelProvider.GenerateAsync(BuildRequest(definition, text, "document-data-entry"), cancellationToken);
        }

        var extraction = ParseExtraction(response.Output, definition);
        var validation = ValidateRows(extraction.Rows, definition);
        run.CandidateRowsJson = JsonSerializer.Serialize(extraction.Rows, JsonOptions);
        run.ValidationJson = JsonSerializer.Serialize(new { valid = validation.Count == 0, issues = validation, notes = extraction.Notes }, JsonOptions);
        run.ModelProvider = response.Provider; run.ModelVersion = response.Model; run.Status = "AwaitingConfirmation"; run.FailureCode = validation.Count == 0 ? null : "DATA_ENTRY_REVIEW_REQUIRED"; run.UpdatedAtUtc = now; run.NextAttemptAtUtc = null; run.CompletedAtUtc = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<DataEntryRunView> ConfirmAsync(Guid runId, string tenantCode, string actorId, string confirmedRowsJson, string? comment = null, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantCode, actorId); if (string.IsNullOrWhiteSpace(confirmedRowsJson) || confirmedRowsJson.Length > 2_000_000) throw new ArgumentException("Confirmed rows payload is empty or too large.", nameof(confirmedRowsJson));
        var run = await db.DataEntryRuns.SingleOrDefaultAsync(x => x.Id == runId && x.TenantCode == tenantCode && x.CreatedBy == actorId, cancellationToken) ?? throw new KeyNotFoundException("DATA_ENTRY_RUN_NOT_FOUND");
        if (run.Status != "AwaitingConfirmation") throw new InvalidOperationException("DATA_ENTRY_RUN_NOT_AWAITING_CONFIRMATION");
        var template = await db.DataEntryTemplates.AsNoTracking().SingleAsync(x => x.Id == run.TemplateId && x.TenantCode == tenantCode, cancellationToken);
        var definition = JsonSerializer.Deserialize<DataEntryTemplateDefinition>(template.DefinitionJson, JsonOptions) ?? throw new InvalidDataException("DATA_ENTRY_TEMPLATE_DEFINITION_INVALID");
        var rows = JsonSerializer.Deserialize<List<DataEntryRow>>(confirmedRowsJson, JsonOptions) ?? throw new JsonException("Confirmed rows must be a JSON array.");
        var issues = ValidateRows(rows, definition); if (issues.Count > 0) throw new InvalidOperationException(JsonSerializer.Serialize(new { code = "DATA_ENTRY_VALIDATION_FAILED", issues }, JsonOptions));
        if (!string.Equals(run.CandidateRowsJson, confirmedRowsJson, StringComparison.Ordinal)) await feedback.RecordAsync(tenantCode, actorId, run.DocumentId, null, "DataEntryCorrection", run.CandidateRowsJson, confirmedRowsJson, comment, cancellationToken);
        run.ConfirmedRowsJson = JsonSerializer.Serialize(rows, JsonOptions); run.Comment = comment; run.Status = "Confirmed"; run.FailureCode = null; run.UpdatedAtUtc = DateTimeOffset.UtcNow; await db.SaveChangesAsync(cancellationToken);
        return await GetRunAsync(runId, tenantCode, actorId, cancellationToken) ?? throw new InvalidOperationException("DATA_ENTRY_RUN_NOT_FOUND_AFTER_CONFIRM");
    }

    public async Task<StoredDataEntryFile?> ExportAsync(Guid runId, string tenantCode, string actorId, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantCode, actorId);
        var run = await db.DataEntryRuns.SingleOrDefaultAsync(x => x.Id == runId && x.TenantCode == tenantCode && x.CreatedBy == actorId, cancellationToken) ?? throw new KeyNotFoundException("DATA_ENTRY_RUN_NOT_FOUND");
        if (run.Status is not ("Confirmed" or "Exported")) throw new InvalidOperationException("DATA_ENTRY_CONFIRMATION_REQUIRED");
        if (!string.IsNullOrWhiteSpace(run.OutputStorageKey))
        {
            var existing = await store.OpenReadAsync(run.OutputStorageKey, cancellationToken);
            return new StoredDataEntryFile(run.OutputFileName ?? $"{run.Id:N}.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", existing, existing.Length);
        }
        var template = await db.DataEntryTemplates.AsNoTracking().SingleAsync(x => x.Id == run.TemplateId && x.TenantCode == tenantCode, cancellationToken);
        var definition = JsonSerializer.Deserialize<DataEntryTemplateDefinition>(template.DefinitionJson, JsonOptions) ?? throw new InvalidDataException("DATA_ENTRY_TEMPLATE_DEFINITION_INVALID");
        var rows = JsonSerializer.Deserialize<List<DataEntryRow>>(run.ConfirmedRowsJson ?? "[]", JsonOptions) ?? [];
        await using var templateStream = await store.OpenReadAsync(template.StorageKey, cancellationToken);
        var bytes = xlsxWriter.Write(templateStream, definition, rows);
        var outputId = Guid.NewGuid(); await using var outputStream = new MemoryStream(bytes, writable: false);
        var stored = await store.SaveAsync(outputId, outputStream, cancellationToken);
        run.OutputStorageKey = stored.StorageKey; run.OutputFileName = BuildOutputName(template.TemplateCode); run.Status = "Exported"; run.CompletedAtUtc = DateTimeOffset.UtcNow; run.UpdatedAtUtc = DateTimeOffset.UtcNow; await db.SaveChangesAsync(cancellationToken);
        var resultStream = await store.OpenReadAsync(stored.StorageKey, cancellationToken);
        return new StoredDataEntryFile(run.OutputFileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", resultStream, resultStream.Length);
    }

    private static AIModelRequest BuildRequest(DataEntryTemplateDefinition definition, string? text, string task) {
        var columns = definition.Columns.Select(x => new { x.Path, x.Header, x.DataType, x.Required, x.SourceHints }).ToArray();
        var input = JsonSerializer.Serialize(new { destination = new { definition.WorksheetName, definition.HeaderRow, definition.DataStartRow, columns }, documentText = text }, JsonOptions);
        return new AIModelRequest(task, input, "Extract document data into rows matching the destination columns. Preserve values faithfully, do not invent missing values, and return only JSON: {\"rows\":[{\"values\":{\"path\":value},\"confidence\":{\"path\":0.0},\"evidence\":{\"path\":\"verbatim source excerpt\"}}],\"notes\":\"...\"}. For repeated table items return one row per item. Confidence is 0..1. Use null only when the source explicitly contains a blank value. Do not perform business approval or database writes.", text is null ? null : null);
    }

    private static DataEntryExtraction ParseExtraction(string output, DataEntryTemplateDefinition definition)
    {
        using var document = JsonDocument.Parse(output); var root = document.RootElement;
        if (!root.TryGetProperty("rows", out var rowsElement) || rowsElement.ValueKind != JsonValueKind.Array || rowsElement.GetArrayLength() > 5000) throw new InvalidDataException("DATA_ENTRY_RESPONSE_INVALID");
        var allowed = definition.Columns.Select(x => x.Path).ToHashSet(StringComparer.OrdinalIgnoreCase); var rows = new List<DataEntryRow>();
        foreach (var row in rowsElement.EnumerateArray())
        {
            if (!row.TryGetProperty("values", out var valuesElement) || valuesElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("DATA_ENTRY_ROW_VALUES_INVALID");
            var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase); foreach (var property in valuesElement.EnumerateObject()) if (allowed.Contains(property.Name)) values[property.Name] = property.Value.Clone();
            var confidence = ReadDoubleMap(row, "confidence"); var evidence = ReadStringMap(row, "evidence"); rows.Add(new DataEntryRow(values, confidence, evidence));
        }
        var notes = root.TryGetProperty("notes", out var notesElement) && notesElement.ValueKind == JsonValueKind.String ? notesElement.GetString() : null;
        return new DataEntryExtraction(rows, notes);
    }

    private static List<object> ValidateRows(IReadOnlyList<DataEntryRow> rows, DataEntryTemplateDefinition definition)
    {
        var issues = new List<object>();
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            foreach (var column in definition.Columns)
            {
                if (!rows[rowIndex].Values.TryGetValue(column.Path, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined || (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString())))
                { if (column.Required) issues.Add(new { row = rowIndex + 1, path = column.Path, code = "REQUIRED_FIELD_MISSING", severity = "Error", message = $"Required field '{column.Header}' is missing." }); continue; }
                var confidenceMap = rows[rowIndex].Confidence;
                if (confidenceMap is not null && confidenceMap.TryGetValue(column.Path, out var confidenceValue) &&
                    (confidenceValue < 0 || confidenceValue > 1 || double.IsNaN(confidenceValue) || double.IsInfinity(confidenceValue)))
                    issues.Add(new { row = rowIndex + 1, path = column.Path, code = "INVALID_CONFIDENCE", severity = "Error", message = "Confidence must be between 0 and 1." });
                if (!MatchesType(value, column.DataType)) issues.Add(new { row = rowIndex + 1, path = column.Path, code = "FIELD_TYPE_MISMATCH", severity = "Error", message = $"Field '{column.Header}' does not match data type '{column.DataType}'." });
            }
        }
        return issues;
    }

    private static bool MatchesType(JsonElement value, string type) => type.ToLowerInvariant() switch
    {
        "integer" => value.ValueKind == JsonValueKind.Number && decimal.TryParse(value.GetRawText(), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) && n == decimal.Truncate(n),
        "decimal" => value.ValueKind == JsonValueKind.Number && decimal.TryParse(value.GetRawText(), NumberStyles.Number, CultureInfo.InvariantCulture, out _),
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "date" => value.ValueKind == JsonValueKind.String && DateOnly.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        "datetime" => value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _),
        "time" => value.ValueKind == JsonValueKind.String && TimeOnly.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        _ => value.ValueKind == JsonValueKind.String
    };

    private static Dictionary<string, double> ReadDoubleMap(JsonElement row, string property) { var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase); if (!row.TryGetProperty(property, out var element) || element.ValueKind != JsonValueKind.Object) return result; foreach (var item in element.EnumerateObject()) if (item.Value.TryGetDouble(out var value)) result[item.Name] = value; return result; }
    private static Dictionary<string, string> ReadStringMap(JsonElement row, string property) { var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); if (!row.TryGetProperty(property, out var element) || element.ValueKind != JsonValueKind.Object) return result; foreach (var item in element.EnumerateObject()) if (item.Value.ValueKind == JsonValueKind.String) result[item.Name] = item.Value.GetString() ?? ""; return result; }
    private static DataEntryTemplateView ToTemplateView(AiDataEntryTemplateRecord x) => new(x.Id, x.TemplateCode, x.DisplayName, x.ModuleCode, x.SchemaCode, x.Version, JsonSerializer.Deserialize<DataEntryTemplateDefinition>(x.DefinitionJson, JsonOptions) ?? throw new InvalidDataException("DATA_ENTRY_TEMPLATE_DEFINITION_INVALID"), x.UpdatedAtUtc);
    private static string BuildOutputName(string templateCode) { var safe = new string(templateCode.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray()); return $"{safe}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.xlsx"; }
    private static void ValidateDefinition(DataEntryTemplateDefinition definition) { if (string.IsNullOrWhiteSpace(definition.WorksheetName) || definition.WorksheetName.Length > 128) throw new ArgumentException("WorksheetName is required and must be <=128 characters."); if (definition.HeaderRow < 1 || definition.DataStartRow < 1 || definition.Columns.Count is < 1 or > 200) throw new ArgumentException("Template rows/columns are invalid."); var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase); foreach (var column in definition.Columns) { ValidateCode(column.Path, 256, "column.Path"); if (!paths.Add(column.Path)) throw new ArgumentException($"Duplicate destination path '{column.Path}'."); if (string.IsNullOrWhiteSpace(column.Header) || column.Header.Length > 256) throw new ArgumentException("Every destination column requires a header."); if (!IsValidColumn(column.ColumnLetter)) throw new ArgumentException($"Invalid XLSX column '{column.ColumnLetter}'."); } }
    private static bool IsValidColumn(string value) => !string.IsNullOrWhiteSpace(value) && value.All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z');
    private static void ValidateIdentity(string tenantCode, string actorId) { if (string.IsNullOrWhiteSpace(tenantCode) || tenantCode.Length > 64) throw new ArgumentException("Invalid tenant code."); if (string.IsNullOrWhiteSpace(actorId) || actorId.Length > 128) throw new ArgumentException("Invalid actor id."); }
    private static void ValidateCode(string value, int max, string name) { if (string.IsNullOrWhiteSpace(value) || value.Length > max) throw new ArgumentException($"{name} is required and must be <= {max} characters.", name); }
}
