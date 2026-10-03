using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FVN_REGISTER.Infrastructure.Models.Data.Configurations.Securities;

public sealed class F03FunctionConfiguration : IEntityTypeConfiguration<F03Function>
{
    public void Configure(EntityTypeBuilder<F03Function> entity)
    {
        entity.ToTable("F03Functions", table =>
        {
            // F03Functions has database triggers. EF Core 9 must not emit
            // UPDATE ... OUTPUT for this table.
            table.UseSqlOutputClause(false);
            // Keep trigger metadata explicit for EF Core versions/providers
            // that use HasTrigger to opt out of OUTPUT.
            table.HasTrigger("TR_F03Functions_EFCore");
        });
        entity.HasKey(e => e.Id);

        entity.HasIndex(e => e.FunctionCode, "IX_Function_Code").IsUnique();

        entity.Property(e => e.FunctionName).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Detail).HasMaxLength(500);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
        entity.Property(e => e.IsActive).HasDefaultValue(true);
    }
}
