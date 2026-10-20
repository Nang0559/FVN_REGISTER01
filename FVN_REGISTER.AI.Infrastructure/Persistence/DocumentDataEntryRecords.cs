using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FVN_REGISTER.AI.Infrastructure.Persistence;

public sealed class AiDataEntryTemplateRecord
{
    public Guid Id { get; set; }
    public string TenantCode { get; set; } = "";
    public string TemplateCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string ModuleCode { get; set; } = "";
    public string SchemaCode { get; set; } = "";
    public int Version { get; set; } = 1;
    public string StorageKey { get; set; } = "";
    public string WorksheetName { get; set; } = "Sheet1";
    public int HeaderRow { get; set; } = 1;
    public int DataStartRow { get; set; } = 2;
    public string DefinitionJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class AiDataEntryRunRecord
{
    public Guid Id { get; set; }
    public string TenantCode { get; set; } = "";
    public Guid DocumentId { get; set; }
    public Guid TemplateId { get; set; }
    public string Status { get; set; } = "WaitingForDocument";
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public DateTimeOffset? LastAttemptAtUtc { get; set; }
    public DateTimeOffset? LastErrorAtUtc { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public string? LeaseOwner { get; set; }
    public string? FailureCode { get; set; }
    public string? CandidateRowsJson { get; set; }
    public string? ValidationJson { get; set; }
    public string? ConfirmedRowsJson { get; set; }
    public string? Comment { get; set; }
    public string? ModelProvider { get; set; }
    public string? ModelVersion { get; set; }
    public string? OutputStorageKey { get; set; }
    public string? OutputFileName { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string CreatedBy { get; set; } = "";
}

public sealed class AiDataEntryTemplateConfiguration : IEntityTypeConfiguration<AiDataEntryTemplateRecord>
{
    public void Configure(EntityTypeBuilder<AiDataEntryTemplateRecord> b)
    {
        b.ToTable("AiDataEntryTemplates", "ai"); b.HasKey(x => x.Id);
        b.Property(x => x.TenantCode).HasMaxLength(64).IsRequired(); b.Property(x => x.TemplateCode).HasMaxLength(128).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(256).IsRequired(); b.Property(x => x.ModuleCode).HasMaxLength(64).IsRequired();
        b.Property(x => x.SchemaCode).HasMaxLength(128).IsRequired(); b.Property(x => x.StorageKey).HasMaxLength(512).IsRequired();
        b.Property(x => x.WorksheetName).HasMaxLength(128).IsRequired(); b.Property(x => x.DefinitionJson).IsRequired(); b.Property(x => x.CreatedBy).HasMaxLength(128).IsRequired();
        b.HasIndex(x => new { x.TenantCode, x.TemplateCode, x.Version }).IsUnique();
        b.HasIndex(x => new { x.TenantCode, x.TemplateCode, x.IsActive });
    }
}

public sealed class AiDataEntryRunConfiguration : IEntityTypeConfiguration<AiDataEntryRunRecord>
{
    public void Configure(EntityTypeBuilder<AiDataEntryRunRecord> b)
    {
        b.ToTable("AiDataEntryRuns", "ai"); b.HasKey(x => x.Id);
        b.Property(x => x.TenantCode).HasMaxLength(64).IsRequired(); b.Property(x => x.Status).HasMaxLength(32).IsRequired();
        b.Property(x => x.LeaseOwner).HasMaxLength(128); b.Property(x => x.FailureCode).HasMaxLength(128); b.Property(x => x.Comment).HasMaxLength(4000);
        b.Property(x => x.ModelProvider).HasMaxLength(64); b.Property(x => x.ModelVersion).HasMaxLength(128); b.Property(x => x.OutputStorageKey).HasMaxLength(512); b.Property(x => x.OutputFileName).HasMaxLength(260); b.Property(x => x.CreatedBy).HasMaxLength(128).IsRequired();
        b.HasIndex(x => new { x.TenantCode, x.Status, x.NextAttemptAtUtc }); b.HasIndex(x => new { x.TenantCode, x.DocumentId, x.CreatedAtUtc });
        b.HasIndex(x => new { x.TenantCode, x.TemplateId, x.CreatedAtUtc });
        b.HasOne<AiDocumentRecord>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AiDataEntryTemplateRecord>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
    }
}
