using FVN_REGISTER.Core.Entities.WorkCalendar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FVN_REGISTER.Infrastructure.Models.Data.Configurations.WorkCalendar;

public sealed class F03SupervisorFeedbackConfiguration : IEntityTypeConfiguration<F03SupervisorFeedback>
{
    public void Configure(EntityTypeBuilder<F03SupervisorFeedback> builder)
    {
        builder.ToTable("F03SupervisorFeedbacks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.EmployeeCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.HasIndex(x => new { x.EmployeeId, x.WorkDate, x.Status });
        builder.HasIndex(x => new { x.ApproverId, x.Status, x.WorkDate });
        builder.HasIndex(x => new { x.EmployeeCode, x.WorkDate, x.Status });
    }
}
