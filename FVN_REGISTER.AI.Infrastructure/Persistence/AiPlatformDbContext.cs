using Microsoft.EntityFrameworkCore;
namespace FVN_REGISTER.AI.Infrastructure.Persistence;
public sealed class AiPlatformDbContext(DbContextOptions<AiPlatformDbContext> options):DbContext(options)
{
 public DbSet<AiDocumentRecord> Documents=>Set<AiDocumentRecord>();
 public DbSet<AiIngestionJobRecord> IngestionJobs=>Set<AiIngestionJobRecord>();
 protected override void OnModelCreating(ModelBuilder b)
 {
  var d=b.Entity<AiDocumentRecord>(); d.ToTable("AiDocuments","ai"); d.HasKey(x=>x.Id);
  d.Property(x=>x.TenantCode).HasMaxLength(64).IsRequired(); d.Property(x=>x.FileName).HasMaxLength(260).IsRequired();
  d.Property(x=>x.Extension).HasMaxLength(12).IsRequired(); d.Property(x=>x.MediaType).HasMaxLength(128).IsRequired();
  d.Property(x=>x.Sha256).HasMaxLength(64).IsRequired(); d.Property(x=>x.StorageKey).HasMaxLength(512).IsRequired();
  d.Property(x=>x.UploadedBy).HasMaxLength(128).IsRequired(); d.Property(x=>x.Status).HasMaxLength(32).IsRequired();
  d.HasIndex(x=>new{x.TenantCode,x.Sha256}).IsUnique(); d.HasIndex(x=>new{x.TenantCode,x.ReceivedAtUtc});
  var j=b.Entity<AiIngestionJobRecord>(); j.ToTable("AiIngestionJobs","ai"); j.HasKey(x=>x.Id);
  j.Property(x=>x.Status).HasMaxLength(24).IsRequired(); j.Property(x=>x.FailureCode).HasMaxLength(128);
  j.HasIndex(x=>new{x.Status,x.CreatedAtUtc}); j.HasIndex(x=>x.DocumentId).IsUnique();
  j.HasOne<AiDocumentRecord>().WithMany().HasForeignKey(x=>x.DocumentId).OnDelete(DeleteBehavior.Restrict);
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
 public DateTimeOffset? CompletedAtUtc{get;set;} public string? FailureCode{get;set;}
}