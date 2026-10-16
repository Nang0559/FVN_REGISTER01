
using System.ComponentModel.DataAnnotations;


namespace FVN_REGISTER.Contract.Dtos.Depts
{
    public class DepartmentUpsertDto
    {
        public int Id { get; set; } // 0 = Tạo mới

        [Required(ErrorMessage = "Mã bộ phận là bắt buộc")]
        [StringLength(20, ErrorMessage = "Mã bộ phận không quá 20 ký tự")]
        [Display(Name = "Mã bộ phận (*)")]
        public int DeptCode { get; set; }

        [Required(ErrorMessage = "Nhập tên bộ phận")]
        [MaxLength(64, ErrorMessage = "Độ dài không quá 64 ký tự")]
        [Display(Name = "Tên bộ phận (*)")]
        public string DeptName { get; set; } = string.Empty;

        [Display(Name = "Kích hoạt")]
        public bool IsActive { get; set; } = true;
    }
}
