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
    /// 1 = Company, 2 = National, 3 = Compensatory, 4 = Other.
    /// This classification is independent from IsPaidLeave/TinhPhep.
    /// </summary>
    public byte HolidayType { get; set; } = 1;

    [JsonIgnore]
    public string HolidayTypeCode => FVN_REGISTER.Contract.Dtos.Calendar.DayTypeCatalog.HolidayTypeCode(HolidayType);

    /// <summary>DayType (rule ký hiệu) đã ánh xạ từ loại ngày nghỉ HRM.</summary>
    public string? DayType { get; set; }

    /// <summary>HRM = đồng bộ từ HRM (nguồn chuẩn); FVN = dòng cũ tự nhập, sẽ bị thay thế.</summary>
    public string Source { get; set; } = "HRM";
}