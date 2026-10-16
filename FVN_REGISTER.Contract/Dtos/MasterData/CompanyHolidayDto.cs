using System.Text.Json.Serialization;

namespace FVN_REGISTER.Contract.Dtos.MasterData;

public class CompanyHolidayDto
{
    public int Id { get; set; }
    public DateTime HolidayDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Year { get; set; }
    public bool IsPaidLeave { get; set; } = true;

    /// <summary>
    /// 1 = Company (CT), 2 = National (L), 4 = Other (NT) theo HRM; 3 không còn dùng.
    /// This classification is independent from IsPaidLeave/TinhPhep.
    /// </summary>
    public byte HolidayType { get; set; } = 1;

    [JsonIgnore]
    public string HolidayTypeCode => FVN_REGISTER.Contract.Dtos.Calendar.DayTypeCatalog.HolidayTypeCode(HolidayType);

    /// <summary>DayType (rule ký hiệu) đã ánh xạ từ loại ngày nghỉ HRM.</summary>
    public string? DayType { get; set; }

    /// <summary>Ký hiệu nghỉ của HRM (L / CT / NT).</summary>
    public string? HolidaySymbol { get; set; }

    /// <summary>Tên loại ngày nghỉ của HRM (DM_Loainghile.LNLTen).</summary>
    public string? HolidayTypeName { get; set; }

    /// <summary>Màu loại ngày nghỉ của HRM (DM_Loainghile.LNLMauSac).</summary>
    public string? HolidayColor { get; set; }

    /// <summary>HRM = đồng bộ từ HRM (nguồn chuẩn); FVN = dòng cũ tự nhập, sẽ bị thay thế.</summary>
    public string Source { get; set; } = "HRM";
}