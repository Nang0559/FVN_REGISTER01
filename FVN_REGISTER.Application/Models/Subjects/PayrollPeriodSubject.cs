using FVN_REGISTER.Application.Interfaces.Approvals;
using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Application.Models.Subjects;

/// <summary>
/// Approval subject for a locked payroll period. The same snapshot represents
/// the finalized attendance/payroll input set for that period; no live rows are
/// substituted while an approval is in progress.
/// </summary>
public sealed class PayrollPeriodSubject : IApprovalSubject
{
    public int RequestId { get; set; }
    public RequestModule Module => RequestModule.Payroll;
    public string EmployeeCode { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public int? DeptCode { get; set; }
    public string? PositionCode { get; set; }
    public ApprovalStatus OverallStatus { get; set; }
    public string PeriodCode { get; set; } = string.Empty;
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string PeriodStatus { get; set; } = string.Empty;
}
