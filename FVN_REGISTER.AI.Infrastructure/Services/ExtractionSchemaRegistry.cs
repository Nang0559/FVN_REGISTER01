using System.Text.Json;
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>Tenant/module-scoped, immutable version registry for extraction schemas.</summary>
public sealed class ExtractionSchemaRegistry(AiPlatformDbContext db) : IExtractionSchemaRegistry
{
    private static readonly HashSet<string> Types = new(StringComparer.OrdinalIgnoreCase)
        { "string", "integer", "decimal", "boolean", "date", "datetime", "time", "email" };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ExtractionSchema?> GetPublishedAsync(string tenantCode, string moduleCode, string schemaCode,
        CancellationToken cancellationToken = default)
    {
        var schema = await db.ExtractionSchemas.AsNoTracking().SingleOrDefaultAsync(x =>
            x.TenantCode == Required(tenantCode, 64) && x.ModuleCode == Required(moduleCode, 64) &&
            x.SchemaCode == Required(schemaCode, 128) && x.IsActive, cancellationToken);
        if (schema is null) return null;
        var version = await db.ExtractionSchemaVersions.AsNoTracking().SingleOrDefaultAsync(x =>
            x.SchemaId == schema.Id && x.Version == schema.CurrentVersion && x.Status == "Published", cancellationToken);
        return version is null ? null : JsonSerializer.Deserialize<ExtractionSchema>(version.DefinitionJson, JsonOptions);
    }

    public async Task<int> CreateDraftVersionAsync(string tenantCode, string moduleCode, ExtractionSchema schema,
        string actorId, CancellationToken cancellationToken = default)
    {
        tenantCode = Required(tenantCode, 64);
        moduleCode = Required(moduleCode, 64);
        actorId = Required(actorId, 128);
        ValidateDefinition(schema);
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var record = await db.ExtractionSchemas.SingleOrDefaultAsync(x =>
            x.TenantCode == tenantCode && x.ModuleCode == moduleCode && x.SchemaCode == schema.SchemaCode,
            cancellationToken);
        if (record is null)
        {
            record = new AiExtractionSchemaRecord
            {
                Id = Guid.NewGuid(), TenantCode = tenantCode, ModuleCode = moduleCode,
                SchemaCode = schema.SchemaCode, DisplayName = schema.DisplayName,
                CreatedBy = actorId, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            db.ExtractionSchemas.Add(record);
        }
        else
        {
            record.DisplayName = schema.DisplayName;
            record.IsActive = true;
            record.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        var latest = await db.ExtractionSchemaVersions.Where(x => x.SchemaId == record.Id)
            .Select(x => (int?)x.Version).MaxAsync(cancellationToken) ?? 0;
        var next = latest + 1;
        var definition = schema with { Version = next };
        db.ExtractionSchemaVersions.Add(new AiExtractionSchemaVersionRecord
        {
            Id = Guid.NewGuid(), SchemaId = record.Id, Version = next,
            DefinitionJson = JsonSerializer.Serialize(definition, JsonOptions),
            Status = "Draft", CreatedBy = actorId, CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return next;
    }

    public async Task PublishVersionAsync(string tenantCode, string moduleCode, string schemaCode, int version,
        string actorId, CancellationToken cancellationToken = default)
    {
        tenantCode = Required(tenantCode, 64);
        moduleCode = Required(moduleCode, 64);
        schemaCode = Required(schemaCode, 128);
        actorId = Required(actorId, 128);
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));

        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var schema = await db.ExtractionSchemas.SingleOrDefaultAsync(x =>
            x.TenantCode == tenantCode && x.ModuleCode == moduleCode && x.SchemaCode == schemaCode,
            cancellationToken) ?? throw new KeyNotFoundException("Extraction schema was not found.");
        var target = await db.ExtractionSchemaVersions.SingleOrDefaultAsync(x =>
            x.SchemaId == schema.Id && x.Version == version, cancellationToken)
            ?? throw new KeyNotFoundException("Extraction schema version was not found.");
        if (target.Status == "Retired") throw new InvalidOperationException("A retired schema version cannot be republished.");

        var previous = await db.ExtractionSchemaVersions.Where(x =>
            x.SchemaId == schema.Id && x.Status == "Published").ToListAsync(cancellationToken);
        foreach (var item in previous) item.Status = "Retired";
        target.Status = "Published";
        target.PublishedAtUtc = DateTimeOffset.UtcNow;
        schema.CurrentVersion = version;
        schema.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static void ValidateDefinition(ExtractionSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        _ = Required(schema.SchemaCode, 128);
        _ = Required(schema.DisplayName, 256);
        if (schema.Fields.Count is < 1 or > 500) throw new ArgumentException("Schema must define between 1 and 500 fields.");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in schema.Fields)
        {
            _ = Required(field.Path, 256);
            if (!paths.Add(field.Path)) throw new ArgumentException($"Duplicate schema field path '{field.Path}'.");
            if (!Types.Contains(field.DataType)) throw new ArgumentException($"Unsupported field type '{field.DataType}'.");
            if (field.MinLength < 0 || field.MaxLength < 0 ||
                (field.MinLength.HasValue && field.MaxLength.HasValue && field.MinLength > field.MaxLength) ||
                (field.Minimum.HasValue && field.Maximum.HasValue && field.Minimum > field.Maximum))
                throw new ArgumentException($"Invalid constraints for field '{field.Path}'.");
            if (field.Pattern is { Length: > 256 }) throw new ArgumentException("Regular expression patterns are limited to 256 characters.");
            if (field.AllowedValues?.Count > 500) throw new ArgumentException("Allowed value lists are limited to 500 entries.");
        }
        foreach (var rule in schema.Rules ?? [])
        {
            if (rule.Code is not ("REQUIRED_TOGETHER" or "MUTUALLY_EXCLUSIVE"))
                throw new ArgumentException($"Unsupported cross-field rule '{rule.Code}'.");
            var members = rule.Expression.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (members.Length < 2 || members.Any(x => !paths.Contains(x)))
                throw new ArgumentException($"Rule '{rule.Code}' references invalid field paths.");
            if (rule.Severity is not ("Error" or "Warning" or "Info"))
                throw new ArgumentException($"Rule '{rule.Code}' has an invalid severity.");
        }
    }

    private static string Required(string value, int max) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max
            ? value.Trim() : throw new ArgumentException($"Value must contain 1 to {max} characters.");
}
