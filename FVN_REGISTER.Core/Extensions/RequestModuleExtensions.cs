using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Core.Extensions;

public static class RequestModuleExtensions
{
    public static string ToDisplayName(this RequestModule module) => module switch
    {
        RequestModule.Leave => "Nghỉ phép",
        RequestModule.Overtime => "Tăng ca",
        RequestModule.Trip => "Công tác",
        RequestModule.Equipment => "Thiết bị",
        RequestModule.Attendance => "Chấm công",
        RequestModule.Payroll => "Tính lương",
        RequestModule.AccessChange => "Thay đổi quyền truy cập",
        RequestModule.Endpoint => "Quản lý thiết bị đầu cuối",
        _ => throw new ArgumentOutOfRangeException(nameof(module), module, "RequestModule chưa được định nghĩa")
    };

    public static string ToCode(this RequestModule module) => module switch
    {
        RequestModule.Leave => "LEAVE",
        RequestModule.Overtime => "OT",
        RequestModule.Trip => "TRIP",
        RequestModule.Equipment => "EQUIPMENT",
        RequestModule.Attendance => "ATTENDANCE",
        RequestModule.Payroll => "PAYROLL",
        RequestModule.AccessChange => "ACCESSCHANGE",
        RequestModule.Endpoint => "ENDPOINT",
        _ => throw new ArgumentOutOfRangeException(nameof(module), module, "RequestModule chưa được định nghĩa")
    };

    public static string ToUnitLabel(this RequestModule module) => module switch
    {
        RequestModule.Overtime => "giờ",
        RequestModule.Equipment => "lần",
        RequestModule.Attendance => "ngày",
        RequestModule.Payroll => "kỳ",
        RequestModule.AccessChange => "lần",
        RequestModule.Endpoint => "thiết bị",
        _ => "ngày"
    };

    public static string ToDetailPath(this RequestModule module) => module switch
    {
        RequestModule.Leave => "/leaves/detail",
        RequestModule.Overtime => "/ot/detail",
        RequestModule.Trip => "/trips/detail",
        RequestModule.Equipment => "/equipment",
        RequestModule.Attendance => "/calendar",
        RequestModule.Payroll => "/payroll",
        RequestModule.AccessChange => "/access-change",
        RequestModule.Endpoint => "/endpoint",
        _ => throw new ArgumentOutOfRangeException(nameof(module), module, "RequestModule chưa được định nghĩa")
    };

    public static RequestModule ParseCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("RequestModule code không được rỗng.", nameof(code));

        return code.Trim().ToUpperInvariant() switch
        {
            "LEAVE" => RequestModule.Leave,
            "OT" => RequestModule.Overtime,
            "TRIP" => RequestModule.Trip,
            "EQUIPMENT" => RequestModule.Equipment,
            "ATTENDANCE" => RequestModule.Attendance,
            "PAYROLL" => RequestModule.Payroll,
            "ACCESSCHANGE" => RequestModule.AccessChange,
            "ENDPOINT" => RequestModule.Endpoint,
            _ => throw new ArgumentException($"RequestModule code không hợp lệ: '{code}'.", nameof(code))
        };
    }

    public static RequestModule ToRequestModule(this string code) => ParseCode(code);

    public static bool TryToRequestModule(this string? code, out RequestModule module)
    {
        module = default;
        if (string.IsNullOrWhiteSpace(code)) return false;

        switch (code.Trim().ToUpperInvariant())
        {
            case "LEAVE": module = RequestModule.Leave; return true;
            case "OT": module = RequestModule.Overtime; return true;
            case "TRIP": module = RequestModule.Trip; return true;
            case "EQUIPMENT": module = RequestModule.Equipment; return true;
            case "ATTENDANCE": module = RequestModule.Attendance; return true;
            case "PAYROLL": module = RequestModule.Payroll; return true;
            case "ACCESSCHANGE": module = RequestModule.AccessChange; return true;
            case "ENDPOINT": module = RequestModule.Endpoint; return true;
            default: return false;
        }
    }
}
