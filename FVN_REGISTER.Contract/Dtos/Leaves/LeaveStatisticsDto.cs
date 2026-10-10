using FVN_REGISTER.Contract.Dtos.Depts;


namespace FVN_REGISTER.Contract.Dtos.Leaves
{
    public class LeaveStatisticsDto
    {
        // --- Phần Thống kê (Header Stats) ---
        public string DepartmentId { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;

        public int TotalEmployees { get; set; }
        public int PresentEmployeesCount { get; set; } // Tổng hiện diện
        public int TotalLeaveCount { get; set; }
        public int ApprovedLeaveCount { get; set; }
        public int PendingLeaveCount { get; set; }

        // Counts for today, populated only for departments covered by the matching
        // active approval policies.
        public int TodayLeaveEmployeesCount { get; set; }
        public int TodayTripEmployeesCount { get; set; }
        public int TodayOTEmployeesCount { get; set; }

        public double LeaveRate { get; set; }
        public double WorkingRate { get; set; }

        // --- Phần Dữ liệu thô (Raw Data) ---
        // Sử dụng LeaveRequestDto (bản rút gọn) thay vì LeaveRequestViewModel cồng kềnh
        public List<LeaveRequestDto> EmployeeLeaves { get; set; } = new();

        // --- Phần Danh mục (Filter/Selection) ---
        // Chỉ nên chứa các option để lọc, không chứa dữ liệu của phòng ban khác
        public List<DepartmentDto> Departments { get; set; } = new();
    }
}
