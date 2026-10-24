using FVN_REGISTER.Core.Entities.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FVN_REGISTER.Infrastructure.Models.Data.Configurations.Common;

public sealed class F03PublicInformationAttachmentConfiguration : IEntityTypeConfiguration<F03PublicInformationAttachment>
{
    public void Configure(EntityTypeBuilder<F03PublicInformationAttachment> b)
    {
        b.ToTable("F03PublicInformationAttachment");
        b.HasKey(x => x.Id);
        b.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        b.Property(x => x.FilePath).HasMaxLength(500).IsRequired();
        b.Property(x => x.FileExtension).HasMaxLength(20);
        b.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
        b.Property(x => x.ModifiedAt).HasColumnType("datetime2(0)");
        b.Ignore(x => x.IsActive);
        b.Ignore(x => x.LastModifiedSource);
        b.HasIndex(x => x.PublicInformationId).HasDatabaseName("IX_F03PublicInformationAttachment_Info");
    }
}
