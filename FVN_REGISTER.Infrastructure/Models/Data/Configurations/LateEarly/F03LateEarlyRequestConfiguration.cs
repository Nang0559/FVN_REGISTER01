using FVN_REGISTER.Core.Entities.LateEarly;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FVN_REGISTER.Infrastructure.Models.Data.Configurations.LateEarly;

public sealed class F03LateEarlyRequestConfiguration : IEntityTypeConfiguration<F03LateEarlyRequest>
{
    public void Configure(EntityTypeBuilder<F03LateEarlyRequest> builder)
    {
        builder.ToTable("F03LateEarlyRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RequestCode).HasMaxLength(30).IsRequired();
        builder.Property(x => x.EmployeeCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.RegisteredByEmployeeCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ReasonCode).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ReasonNote).HasMaxLength(500);
        builder.Property(x => x.CancelReason).HasMaxLength(500);
        builder.Property(x => x.WorkDate).HasColumnType("date");
        builder.Property(x => x.PlannedTime).HasColumnType("time(0)");
        builder.HasIndex(x => x.RequestCode).IsUnique();
        builder.HasIndex(x => new { x.EmployeeCode, x.WorkDate, x.LateEarlyType });
    }
}
