using FVN_REGISTER.Core.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FVN_REGISTER.Infrastructure.Models.Data.Configurations;

public sealed class F03EndpointAntivirusInventoryConfiguration : IEntityTypeConfiguration<F03EndpointAntivirusInventory>
{
    public void Configure(EntityTypeBuilder<F03EndpointAntivirusInventory> entity)
    {
        entity.ToTable("F03EndpointAntivirusInventory");
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => new { x.EndpointDeviceId, x.ProductName });
        entity.HasIndex(x => new { x.EndpointDeviceId, x.DefinitionUpdatedAtUtc });
        entity.Property(x => x.ProductName).HasMaxLength(255).IsRequired();
        entity.Property(x => x.ProductVersion).HasMaxLength(100);
        entity.Property(x => x.EngineVersion).HasMaxLength(100);
        entity.Property(x => x.DefinitionVersion).HasMaxLength(100);
        entity.Property(x => x.ProtectionStatus).HasMaxLength(50);
        entity.Property(x => x.RunningMode).HasMaxLength(50);
        entity.Property(x => x.Source).HasMaxLength(30).IsRequired();
    }
}
