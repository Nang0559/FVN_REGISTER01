using FVN_REGISTER.Core.Entities.Approvers;
using FVN_REGISTER.Core.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FVN_REGISTER.Infrastructure.Models.Data.Configurations.Approvers;

public sealed class F03ApproverConfiguration : IEntityTypeConfiguration<F03Approver>
{
    public void Configure(EntityTypeBuilder<F03Approver> entity)
    {
        entity.ToTable("F03Approvers");
        entity.HasKey(e => e.Id);

        entity.HasIndex(e => new { e.RequestType, e.ApproveForDeptCode }, "IX_Approver_Lookup");
        entity.HasIndex(e => e.UserId, "IX_Approver_UserId");

        entity.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // RequestType is persisted as the enum name (Leave / Overtime / Trip / Equipment),
        // the same value usp_ReconcileEmployeeApprovers (SQL/31) writes. Reading goes through
        // ParseCode, which is fail-fast: an unknown DB value must never become Leave.
        entity.Property(e => e.RequestType)
            .HasConversion(
                v => v.ToStorageName(),
                v => RequestModuleExtensions.ParseCode(v))
            .IsRequired()
            .HasMaxLength(20);

        entity.Property(e => e.ApproverCode).IsRequired().HasMaxLength(50);
        entity.Property(e => e.ApproveForDeptCode).IsRequired().HasMaxLength(20);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
        entity.Property(e => e.IsActive).HasDefaultValue(true);
    }
}
