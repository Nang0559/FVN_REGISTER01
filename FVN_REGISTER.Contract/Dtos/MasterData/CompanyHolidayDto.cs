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
    public string HolidayTypeCode => HolidayType switch
    {
        2 => "NATIONAL",
        3 => "COMPENSATORY",
        4 => "OTHER",
        _ => "COMPANY"
    };
}