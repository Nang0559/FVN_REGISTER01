

using FVN_REGISTER.Application.Interfaces.Approvals;
using FVN_REGISTER.Core.Entities.Leaves;
using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Application.Models.Subjects
{
    public sealed class LeaveRequestSubject : IApprovalSubject
    {
        public int RequestId { get; set; }
        public RequestModule Module => RequestModule.Leave;
        public string EmployeeCode { get; set; } = "";
        public string? EmployeeName { get; set; }        // ← THÊM
        public int? DeptCode { get; set; }
        public string? PositionCode { get; set; }
        public ApprovalStatus OverallStatus { get; set; }

        public int WorkYear { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalDay { get; set; }
        public string? LeaveReason { get; set; }
        public string? LeaveTypeCode { get; set; }

        // employeeName là tham số bắt buộc vì F03LeaveDay không lưu tên nhân viên trên entity —
        // Provider (GetSubjectAsync) phải join F03Employee/F03Department rồi truyền vào đây.
        public static LeaveRequestSubject From(F03LeaveDay x, string? employeeName = null, int? deptCode = null) => new()
        {
            RequestId = x.Id,
            EmployeeCode = x.EmployeeCode,
            EmployeeName = employeeName,
            DeptCode = deptCode ?? (int.TryParse(x.DeptCode, out var parsedDeptCode) ? parsedDeptCode : null),   // giữ tương thích nếu sau này F03LeaveDay có DeptCode
            PositionCode = null, // Leave thường không dùng CvCode để duyệt
            OverallStatus = x.RequestStatus,
            WorkYear = x.WorkYear,
            StartDate = x.StartDate,
            EndDate = x.EndDate,
            TotalDay = x.TotalDay,
            LeaveReason = x.LeaveReason,
            LeaveTypeCode = x.LeaveTypeCode
        };
    }
}
