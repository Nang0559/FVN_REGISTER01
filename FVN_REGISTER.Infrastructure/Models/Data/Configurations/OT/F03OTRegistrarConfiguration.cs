using FVN_REGISTER.Core.Entities.OT;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FVN_REGISTER.Infrastructure.Models.Data.Configurations.OT;

public sealed class F03OTRegistrarConfiguration : IEntityTypeConfiguration<F03OTRegistrar>
{
    public void Configure(EntityTypeBuilder<F03OTRegistrar> builder)
    {
        builder.ToTable("F03OTRegistrars");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RegisterType).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.RegistrationRuleId, x.UserId, x.RegisterType }).IsUnique();
        builder.HasIndex(x => new { x.DepartmentCode, x.UserId, x.RegisterType });
    }
}
