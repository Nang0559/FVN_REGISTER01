using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FVN_REGISTER.Infrastructure.Models.Data.Configurations.Securities;

public sealed class F03UserConfiguration : IEntityTypeConfiguration<F03User>
{
    public void Configure(EntityTypeBuilder<F03User> entity)
    {
        entity.Property(e => e.DeptCode).HasMaxLength(20);
        entity.ToTable("F03Users");
        entity.HasKey(e => e.Id);

        entity.HasIndex(e => e.EmployeeCode, "IX_User_EmployeeCode").IsUnique();

        entity.Property(e => e.EmployeeCode).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Password).IsRequired().HasMaxLength(255);
        entity.Property(e => e.FullName).HasMaxLength(100);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
        entity.Property(e => e.IsActive).HasDefaultValue(true);

        // F03Users.PermissionCode tham chiếu F03Permissions.PermissionCode (business key),
        // KHÔNG phải F03Permissions.Id. Thiếu dòng này EF join theo Id.
        entity.HasOne(e => e.PermissionCodeNavigation)
              .WithMany(p => p.F03users)
              .HasForeignKey(e => e.PermissionCode)
              .HasPrincipalKey(p => p.PermissionCode)
              .OnDelete(DeleteBehavior.Restrict);
    }
}
