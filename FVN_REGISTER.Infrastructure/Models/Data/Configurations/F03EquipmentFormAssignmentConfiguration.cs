using FVN_REGISTER.Core.Entities.Equipment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FVN_REGISTER.Infrastructure.Models.Data.Configurations;

public sealed class F03EquipmentFormAssignmentConfiguration : IEntityTypeConfiguration<F03EquipmentFormAssignment>
{
    public void Configure(EntityTypeBuilder<F03EquipmentFormAssignment> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.EquipmentSchemaKey).IsRequired().HasMaxLength(64);
        entity.HasIndex(x => new { x.EquipmentSchemaKey, x.FormId }).IsUnique();
        entity.HasIndex(x => new { x.EquipmentSchemaKey, x.IsActiveAssignment });
        entity.HasOne(x => x.Form)
            .WithMany()
            .HasForeignKey(x => x.FormId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
