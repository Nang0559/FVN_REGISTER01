using FVN_REGISTER.Core.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace FVN_REGISTER.Infrastructure.Models.Data.Configurations.Common
{
    public class F03EscalationRuleConfiguration : IEntityTypeConfiguration<F03EscalationRule>
    {
        public void Configure(EntityTypeBuilder<F03EscalationRule> entity)
        {
            entity.ToTable("F03EscalationRules");
            entity.HasKey(e => e.Id).HasName("PK_F03EscalationRules");

            // Index: Tối ưu khi hệ thống cần kiểm tra quy tắc cho phòng ban cụ thể
            entity.HasIndex(e => new { e.RequestModule, e.Level, e.DeptCode }, "IX_EscalationRule_Lookup");

            // RequestModule được lưu bằng mã canonical (LEAVE, OT, TRIP, ...) giống F03Approver
            // và dữ liệu seed (SQL/06_Seed.sql). Không dùng HasConversion<string>() vì nó ghi/đọc
            // theo tên enum ("Overtime") nên không đọc được giá trị 'OT' trong DB.
            // ParseCode vẫn chấp nhận dữ liệu cũ như 'Overtime', và fail-fast nếu gặp giá trị lạ.
            entity.Property(e => e.RequestModule)
                  .HasConversion(
                      v => v.ToCode(),
                      v => RequestModuleExtensions.ParseCode(v))
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.WarningHours).HasColumnType("decimal(5,2)");
            entity.Property(e => e.EscalateHours).HasColumnType("decimal(5,2)");

            // Audit defaults
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        }
    }
}
