using FVN_REGISTER.AI.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
namespace FVN_REGISTER.AI.Infrastructure.Persistence;
public sealed class AiPlatformDbContext(DbContextOptions<AiPlatformDbContext> options):DbContext(options)
{
 public DbSet<AiDocumentRecord> Documents=>Set<AiDocumentRecord>();
 public DbSet<AiIngestionJobRecord> IngestionJobs=>Set<AiIngestionJobRecord>();
 public DbSet<AiExtractionRecord> Extractions=>Set<AiExtractionRecord>();
 public DbSet<AiReviewRecord> Reviews=>Set<AiReviewRecord>();
 public DbSet<AiFeedbackRecord> Feedback=>Set<AiFeedbackRecord>();
 public DbSet<AiKnowledgeChunkRecord> KnowledgeChunks=>Set<AiKnowledgeChunkRecord>();
 public DbSet<AiExtractionSchemaRecord> ExtractionSchemas=>Set<AiExtractionSchemaRecord>();
 public DbSet<AiExtractionSchemaVersionRecord> ExtractionSchemaVersions=>Set<AiExtractionSchemaVersionRecord>();
 public DbSet<AiValidationIssueRecord> ValidationIssues=>Set<AiValidationIssueRecord>();
 public DbSet<AiHRCaseRecord> HRCases=>Set<AiHRCaseRecord>();
 public DbSet<AiHRCaseReviewRecord> HRCaseReviews=>Set<AiHRCaseReviewRecord>();
 protected override void OnModelCreating(ModelBuilder b)
 {
  var d=b.Entity<AiDocumentRecord>(); d.ToTable("AiDocuments","ai"); d.HasKey(x=>x.Id);
  d.Property(x=>x.TenantCode).HasMaxLength(64).IsRequired(); d.Property(x=>x.FileName).HasMaxLength(260).IsRequired();
  d.Property(x=>x.Extension).HasMaxLength(12).IsRequired(); d.Property(x=>x.MediaType).HasMaxLength(128).IsRequired();
  d.Property(x=>x.Sha256).HasMaxLength(64).IsRequired(); d.Property(x=>x.StorageKey).HasMaxLength(512).IsRequired();
  d.Property(x=>x.UploadedBy).HasMaxLength(128).IsRequired(); d.Property(x=>x.Status).HasMaxLength(32).IsRequired();
  d.Property(x=>x.ModuleCode).HasMaxLength(64); d.Property(x=>x.SchemaCode).HasMaxLength(128);
  d.HasIndex(x=>new{x.TenantCode,x.ModuleCode,x.SchemaCode,x.ReceivedAtUtc});
  d.HasIndex(x=>new{x.TenantCode,x.Sha256}).IsUnique(); d.HasIndex(x=>new{x.TenantCode,x.ReceivedAtUtc});
  var j=b.Entity<AiIngestionJobRecord>(); j.ToTable("AiIngestionJobs","ai"); j.HasKey(x=>x.Id);
  j.Property(x=>x.Status).HasMaxLength(24).IsRequired(); j.Property(x=>x.FailureCode).HasMaxLength(128); j.Property(x=>x.LeaseOwner).HasMaxLength(128);
  j.HasIndex(x=>new{x.Status,x.CreatedAtUtc}); j.HasIndex(x=>x.DocumentId).IsUnique();
  j.HasOne<AiDocumentRecord>().WithMany().HasForeignKey(x=>x.DocumentId).OnDelete(DeleteBehavior.Restrict);
  var e=b.Entity<AiExtractionRecord>(); e.ToTable("AiExtractions","ai"); e.HasKey(x=>x.Id);
  e.Property(x=>x.TenantCode).HasMaxLength(64).IsRequired(); e.Property(x=>x.DocumentType).HasMaxLength(128).IsRequired();
  e.Property(x=>x.CandidateJson).IsRequired(); e.Property(x=>x.ParserVersion).HasMaxLength(64).IsRequired();
  e.HasIndex(x=>new{x.TenantCode,x.DocumentId,x.CreatedAtUtc});
  var vi=b.Entity<AiValidationIssueRecord>(); vi.ToTable("AiValidationIssues","ai"); vi.HasKey(x=>x.Id);
  vi.Property(x=>x.FieldPath).HasMaxLength(256); vi.Property(x=>x.RuleCode).HasMaxLength(128).IsRequired();
  vi.Property(x=>x.Severity).HasMaxLength(16).IsRequired(); vi.Property(x=>x.Message).HasMaxLength(1000).IsRequired();
  vi.HasIndex(x=>new{x.ExtractionId,x.Severity}); vi.HasOne<AiExtractionRecord>().WithMany().HasForeignKey(x=>x.ExtractionId).OnDelete(DeleteBehavior.Restrict);
  var r=b.Entity<AiReviewRecord>(); r.ToTable("AiReviews","ai"); r.HasKey(x=>x.Id);
  r.Property(x=>x.TenantCode).HasMaxLength(64).IsRequired(); r.Property(x=>x.ReviewerId).HasMaxLength(128).IsRequired();
  r.Property(x=>x.Decision).HasMaxLength(16).IsRequired(); r.Property(x=>x.OriginalPayloadJson).IsRequired();
  r.HasIndex(x=>new{x.TenantCode,x.DocumentId,x.ReviewedAtUtc});
  var h=b.Entity<AiHRCaseRecord>(); h.ToTable("AiHRCases","ai"); h.HasKey(x=>x.Id);
  h.Property(x=>x.TenantCode).HasMaxLength(64).IsRequired(); h.Property(x=>x.EmployeeCode).HasMaxLength(64).IsRequired();
  h.Property(x=>x.CaseType).HasMaxLength(24).IsRequired(); h.Property(x=>x.Outcome).HasMaxLength(32).IsRequired();
  h.Property(x=>x.UserStatement).HasMaxLength(8000).IsRequired(); h.Property(x=>x.EvidenceReference).HasMaxLength(512);
  h.Property(x=>x.AnalysisJson).IsRequired(); h.Property(x=>x.SuggestedReply).HasMaxLength(4000).IsRequired();
  h.Property(x=>x.InternalHRNote).HasMaxLength(8000).IsRequired(); h.Property(x=>x.OfficialSource).HasMaxLength(128).IsRequired(); h.Property(x=>x.OfficialRecordReference).HasMaxLength(256).IsRequired(); h.Property(x=>x.Status).HasMaxLength(24).IsRequired();
  h.Property(x=>x.CreatedBy).HasMaxLength(128).IsRequired(); h.Property(x=>x.ReviewedBy).HasMaxLength(128);
  h.Property(x=>x.ReviewDecision).HasMaxLength(16); h.Property(x=>x.RowVersion).IsRowVersion();
  h.HasIndex(x=>new{x.TenantCode,x.WorkDate,x.CaseType});
  h.HasIndex(x=>new{x.TenantCode,x.Status,x.CreatedAtUtc});
  var hr=b.Entity<AiHRCaseReviewRecord>(); hr.ToTable("AiHRCaseReviews","ai"); hr.HasKey(x=>x.Id);
  hr.Property(x=>x.TenantCode).HasMaxLength(64).IsRequired(); hr.Property(x=>x.ReviewerId).HasMaxLength(128).IsRequired();
  hr.Property(x=>x.Decision).HasMaxLength(16).IsRequired(); hr.Property(x=>x.FinalReply).HasMaxLength(4000);
  hr.Property(x=>x.Comment).HasMaxLength(2000); hr.HasIndex(x=>new{x.TenantCode,x.CaseId,x.ReviewedAtUtc});
  hr.HasOne<AiHRCaseRecord>().WithMany().HasForeignKey(x=>x.CaseId).OnDelete(DeleteBehavior.Restrict);
  var f=b.Entity<AiFeedbackRecord>(); f.ToTable("AiFeedback","ai"); f.HasKey(x=>x.Id);
  f.Property(x=>x.TenantCode).HasMaxLength(64).IsRequired(); f.Property(x=>x.ReviewerId).HasMaxLength(128).IsRequired();
  f.Property(x=>x.FeedbackType).HasMaxLength(24).IsRequired(); f.Property(x=>x.Reason).HasMaxLength(2000);
  f.Property(x=>x.ParserVersion).HasMaxLength(64); f.Property(x=>x.ModelVersion).HasMaxLength(128); f.Property(x=>x.PromptVersion).HasMaxLength(64);
  f.HasIndex(x=>new{x.TenantCode,x.CreatedAtUtc});
  var k=b.Entity<AiKnowledgeChunkRecord>(); k.ToTable("AiKnowledgeChunks","ai"); k.HasKey(x=>x.Id);
  k.Property(x=>x.TenantCode).HasMaxLength(64).IsRequired(); k.Property(x=>x.SectionPath).HasMaxLength(512);
  k.Property(x=>x.ContentHash).HasMaxLength(64).IsRequired(); k.Property(x=>x.AccessScopeJson).IsRequired();
  k.Property(x=>x.EmbeddingModel).HasMaxLength(128); k.Property(x=>x.EmbeddingJson);
  k.HasIndex(x=>new{x.TenantCode,x.DocumentId,x.IsActive});
  k.HasIndex(x=>new{x.TenantCode,x.DocumentId,x.ChunkOrdinal}).IsUnique();
  k.HasOne<AiDocumentRecord>().WithMany().HasForeignKey(x=>x.DocumentId).OnDelete(DeleteBehavior.Restrict);
  k.HasOne<AiReviewRecord>().WithMany().HasForeignKey(x=>x.ReviewId).OnDelete(DeleteBehavior.Restrict);
  var s=b.Entity<AiExtractionSchemaRecord>(); s.ToTable("AiExtractionSchemas","ai"); s.HasKey(x=>x.Id);
  s.Property(x=>x.TenantCode).HasMaxLength(64).IsRequired(); s.Property(x=>x.ModuleCode).HasMaxLength(64).IsRequired();
  s.Property(x=>x.SchemaCode).HasMaxLength(128).IsRequired(); s.Property(x=>x.DisplayName).HasMaxLength(256).IsRequired();
  s.Property(x=>x.CreatedBy).HasMaxLength(128).IsRequired(); s.HasIndex(x=>new{x.TenantCode,x.ModuleCode,x.SchemaCode}).IsUnique();
  var sv=b.Entity<AiExtractionSchemaVersionRecord>(); sv.ToTable("AiExtractionSchemaVersions","ai"); sv.HasKey(x=>x.Id);
  sv.Property(x=>x.Status).HasMaxLength(16).IsRequired(); sv.Property(x=>x.CreatedBy).HasMaxLength(128).IsRequired();
  sv.Property(x=>x.DefinitionJson).IsRequired(); sv.HasIndex(x=>new{x.SchemaId,x.Version}).IsUnique();
  sv.HasOne<AiExtractionSchemaRecord>().WithMany().HasForeignKey(x=>x.SchemaId).OnDelete(DeleteBehavior.Restrict);
 }
}
public sealed class AiDocumentRecord
{
 public Guid Id{get;set;} public string TenantCode{get;set;}=string.Empty; public string FileName{get;set;}=string.Empty;
 public string Extension{get;set;}=string.Empty; public string MediaType{get;set;}=string.Empty; public long Length{get;set;}
 public string Sha256{get;set;}=string.Empty; public string StorageKey{get;set;}=string.Empty; public string UploadedBy{get;set;}=string.Empty;
 public string? ModuleCode{get;set;} public string? SchemaCode{get;set;}
 public DateTimeOffset ReceivedAtUtc{get;set;} public string Status{get;set;}="Quarantined";
}
public sealed class AiIngestionJobRecord
{
 public Guid Id{get;set;} public Guid DocumentId{get;set;} public string Status{get;set;}="Queued";
 public int AttemptCount{get;set;} public DateTimeOffset CreatedAtUtc{get;set;} public DateTimeOffset? StartedAtUtc{get;set;}
 public string? LeaseOwner{get;set;} public DateTimeOffset? LeaseUntilUtc{get;set;}
 public DateTimeOffset? CompletedAtUtc{get;set;} public string? FailureCode{get;set;}
}
public sealed class AiHRCaseRecord
{
 public Guid Id{get;set;} public string TenantCode{get;set;}="";
 public string EmployeeCode{get;set;}=""; public string CaseType{get;set;}="";
 public DateOnly WorkDate{get;set;} public string Outcome{get;set;}="";
 public string UserStatement{get;set;}=""; public string? EvidenceReference{get;set;} public string? EvidenceCandidateJson { get; set; }
 public string AnalysisJson{get;set;}="{}"; public string SuggestedReply{get;set;}="";
 public string InternalHRNote{get;set;}=""; public string OfficialSource{get;set;}="";
 public string OfficialRecordReference{get;set;}=""; public DateTimeOffset FactsRetrievedAtUtc{get;set;}
 public string Status{get;set;}="PendingHRReview"; public string CreatedBy{get;set;}="";
 public DateTimeOffset CreatedAtUtc{get;set;} public string? ReviewedBy{get;set;}
 public string? ReviewDecision{get;set;} public string? FinalReply{get;set;}
 public string? ReviewComment{get;set;} public DateTimeOffset? ReviewedAtUtc{get;set;} public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public sealed class AiHRCaseReviewRecord
{
 public Guid Id { get; set; } public Guid CaseId { get; set; } public string TenantCode { get; set; } = "";
 public string ReviewerId { get; set; } = ""; public string Decision { get; set; } = "";
 public string? FinalReply { get; set; } public string? Comment { get; set; }
 public DateTimeOffset ReviewedAtUtc { get; set; }
}

public sealed class AiKnowledgeChunkRecord
{
 public Guid Id{get;set;} public string TenantCode{get;set;}="";
 public Guid DocumentId{get;set;} public Guid ReviewId{get;set;} public int ChunkOrdinal{get;set;}
 public int? PageNumber{get;set;} public string? SectionPath{get;set;} public string Content{get;set;}="";
 public string ContentHash{get;set;}=""; public string AccessScopeJson{get;set;}="[]";
 public string? EmbeddingModel{get;set;} public string? EmbeddingJson{get;set;}
 public bool IsActive{get;set;}=true; public DateTimeOffset CreatedAtUtc{get;set;}
}

public sealed class AiExtractionSchemaRecord
{
 public Guid Id{get;set;} public string TenantCode{get;set;}=""; public string ModuleCode{get;set;}="";
 public string SchemaCode{get;set;}=""; public string DisplayName{get;set;}="";
 public int CurrentVersion{get;set;}=1; public bool IsActive{get;set;}=true;
 public string CreatedBy{get;set;}=""; public DateTimeOffset CreatedAtUtc{get;set;} public DateTimeOffset UpdatedAtUtc{get;set;}
}
public sealed class AiExtractionSchemaVersionRecord
{
 public Guid Id{get;set;} public Guid SchemaId{get;set;} public int Version{get;set;}
 public string DefinitionJson{get;set;}="{}"; public string Status{get;set;}="Draft";
 public string CreatedBy{get;set;}=""; public DateTimeOffset CreatedAtUtc{get;set;} public DateTimeOffset? PublishedAtUtc{get;set;}
}

public sealed class AiValidationIssueRecord
{
 public Guid Id{get;set;} public Guid ExtractionId{get;set;} public string? FieldPath{get;set;}
 public string RuleCode{get;set;}=""; public string Severity{get;set;}="Error";
 public string Message{get;set;}=""; public DateTimeOffset CreatedAtUtc{get;set;}
}
