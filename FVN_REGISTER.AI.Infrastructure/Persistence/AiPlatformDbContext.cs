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
 protected override void OnModelCreating(ModelBuilder b)
 {
  var d=b.Entity<AiDocumentRecord>(); d.ToTable("AiDocuments","ai"); d.HasKey(x=>x.Id);
  d.Property(x=>x.TenantCode).HasMaxLength(64).IsRequired(); d.Property(x=>x.FileName).HasMaxLength(260).IsRequired();
  d.Property(x=>x.Extension).HasMaxLength(12).IsRequired(); d.Property(x=>x.MediaType).HasMaxLength(128).IsRequired();
  d.Property(x=>x.Sha256).HasMaxLength(64).IsRequired(); d.Property(x=>x.StorageKey).HasMaxLength(512).IsRequired();
  d.Property(x=>x.UploadedBy).HasMaxLength(128).IsRequired(); d.Property(x=>x.Status).HasMaxLength(32).IsRequired();
  d.HasIndex(x=>new{x.TenantCode,x.Sha256}).IsUnique(); d.HasIndex(x=>new{x.TenantCode,x.ReceivedAtUtc});
  var j=b.Entity<AiIngestionJobRecord>(); j.ToTable("AiIngestionJobs","ai"); j.HasKey(x=>x.Id);
  j.Property(x=>x.Status).HasMaxLength(24).IsRequired(); j.Property(x=>x.FailureCode).HasMaxLength(128); j.Property(x=>x.LeaseOwner).HasMaxLength(128);
  j.HasIndex(x=>new{x.Status,x.CreatedAtUtc}); j.HasIndex(x=>x.DocumentId).IsUnique();
  j.HasOne<AiDocumentRecord>().WithMany().HasForeignKey(x=>x.DocumentId).OnDelete(DeleteBehavior.Restrict);
  var e=b.Entity<AiExtractionRecord>(); e.ToTable("AiExtractions","ai"); e.HasKey(x=>x.Id);
  e.Property(x=>x.TenantCode).HasMaxLength(64).IsRequired(); e.Property(x=>x.DocumentType).HasMaxLength(128).IsRequired();
  e.Property(x=>x.CandidateJson).IsRequired(); e.Property(x=>x.ParserVersion).HasMaxLength(64).IsRequired();
  e.HasIndex(x=>new{x.TenantCode,x.DocumentId,x.CreatedAtUtc});
  var r=b.Entity<AiReviewRecord>(); r.ToTable("AiReviews","ai"); r.HasKey(x=>x.Id);
  r.Property(x=>x.TenantCode).HasMaxLength(64).IsRequired(); r.Property(x=>x.ReviewerId).HasMaxLength(128).IsRequired();
  r.Property(x=>x.Decision).HasMaxLength(16).IsRequired(); r.Property(x=>x.OriginalPayloadJson).IsRequired();
  r.HasIndex(x=>new{x.TenantCode,x.DocumentId,x.ReviewedAtUtc});
  var f=b.Entity<AiFeedbackRecord>(); f.ToTable("AiFeedback","ai"); f.HasKey(x=>x.Id);
  f.Property(x=>x.TenantCode).HasMaxLength(64).IsRequired(); f.Property(x=>x.ReviewerId).HasMaxLength(128).IsRequired();
  f.Property(x=>x.FeedbackType).HasMaxLength(24).IsRequired(); f.Property(x=>x.Reason).HasMaxLength(2000);
  f.HasIndex(x=>new{x.TenantCode,x.CreatedAtUtc});
 }
}
public sealed class AiDocumentRecord
{
 public Guid Id{get;set;} public string TenantCode{get;set;}=string.Empty; public string FileName{get;set;}=string.Empty;
 public string Extension{get;set;}=string.Empty; public string MediaType{get;set;}=string.Empty; public long Length{get;set;}
 public string Sha256{get;set;}=string.Empty; public string StorageKey{get;set;}=string.Empty; public string UploadedBy{get;set;}=string.Empty;
 public DateTimeOffset ReceivedAtUtc{get;set;} public string Status{get;set;}="Quarantined";
}
public sealed class AiIngestionJobRecord
{
 public Guid Id{get;set;} public Guid DocumentId{get;set;} public string Status{get;set;}="Queued";
 public int AttemptCount{get;set;} public DateTimeOffset CreatedAtUtc{get;set;} public DateTimeOffset? StartedAtUtc{get;set;}
 public string? LeaseOwner{get;set;} public DateTimeOffset? LeaseUntilUtc{get;set;}
 public DateTimeOffset? CompletedAtUtc{get;set;} public string? FailureCode{get;set;}
}