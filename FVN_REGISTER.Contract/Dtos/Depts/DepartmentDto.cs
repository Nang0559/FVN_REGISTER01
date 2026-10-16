

namespace FVN_REGISTER.Contract.Dtos.Depts
{
    public class DepartmentDto
    {
        public int Id { get; set; }
        public int DeptCode { get; set; }
        public string DeptName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        // Chỉ các thông tin Read-only mới đưa vào đây
        public DateTime CreatedAt { get; set; }
        public int EmployeeCount { get; set; }
    }
}
