using FVN_REGISTER.Core.Entities.OT;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FVN_REGISTER.Infrastructure.Models.Data.Configurations.OT;

public sealed class F03OTRegistrarPositionConfiguration : IEntityTypeConfiguration<F03OTRegistrarPosition>
{
    public void Configure(EntityTypeBuilder<F03OTRegistrarPosition> builder)
    {
        builder.ToTable("F03OTRegistrarPositions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PositionCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.RegisterType).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.RegistrationRuleId, x.PositionCode, x.RegisterType }).IsUnique();
        builder.HasIndex(x => new { x.DepartmentCode, x.PositionCode, x.RegisterType });
    }
}
