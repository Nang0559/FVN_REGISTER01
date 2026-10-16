using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FVN_REGISTER.Core.Entities.Common;

[Table("F03CompanyHolidays")]
public partial class F03CompanyHoliday : BaseAuditEntity
{
    [Required]
    public DateTime HolidayDate { get; set; }

    [Required, StringLength(200)]
    public string Description { get; set; } = string.Empty;

    public int Year { get; set; }

    /// <summary>
    /// 1 = ngày nghỉ được tính vào chế độ phép theo cấu hình HR.
    /// 0 = ngày nghỉ nhưng không tính phép.
    /// </summary>
    public bool TinhPhep { get; set; }

    /// <summary>
    /// Phân loại ngày nghỉ dùng cho lịch công:
    /// 1 = Company (CT), 2 = National (L), 4 = Other (NT); 3 (nghỉ bù) không còn dùng.
    /// </summary>
    public byte HolidayType { get; set; } = 1;

    /// <summary>HRM = đồng bộ từ HRM.dbo.tblNgayNghiLe (nguồn chuẩn).</summary>
    [Required, StringLength(20)]
    public string Source { get; set; } = "FVN";

    /// <summary>tblNgayNghiLe.NNLMa.</summary>
    [StringLength(10)]
    public string? HrmHolidayCode { get; set; }

    /// <summary>tblNgayNghiLe.NNLLoai.</summary>
    [StringLength(5)]
    public string? HrmTypeCode { get; set; }

    /// <summary>DayType của rule ký hiệu (NATIONAL_HOLIDAY, COMPANY_HOLIDAY, OTHER).</summary>
    [StringLength(40)]
    public string? DayType { get; set; }

    /// <summary>Ký hiệu nghỉ của HRM (DM_Loainghile.LNLKyHieu): L, CT, NT.</summary>
    [StringLength(50)]
    public string? HolidaySymbol { get; set; }

    /// <summary>Tên loại ngày nghỉ của HRM (DM_Loainghile.LNLTen).</summary>
    [StringLength(200)]
    public string? HolidayTypeName { get; set; }

    /// <summary>Màu loại ngày nghỉ của HRM (DM_Loainghile.LNLMauSac), ví dụ Red, Crimson.</summary>
    [StringLength(50)]
    public string? HolidayColor { get; set; }
}
