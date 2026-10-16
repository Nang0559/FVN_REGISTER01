using FVN_REGISTER.Core.Entities.OT;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FVN_REGISTER.Infrastructure.Models.Data.Configurations.OT;

public sealed class F03OTRegistrationRuleConfiguration : IEntityTypeConfiguration<F03OTRegistrationRule>
{
    public void Configure(EntityTypeBuilder<F03OTRegistrationRule> builder)
    {
        builder.ToTable("F03OTRegistrationRules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Mode).HasConversion<int>().IsRequired();
        builder.Property(x => x.RegisterType).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.DepartmentCode, x.RegisterType }).IsUnique();
        builder.HasMany(x => x.Registrars).WithOne(x => x.RegistrationRule).HasForeignKey(x => x.RegistrationRuleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.RegistrarPositions).WithOne(x => x.RegistrationRule).HasForeignKey(x => x.RegistrationRuleId).OnDelete(DeleteBehavior.Cascade);
    }
}
