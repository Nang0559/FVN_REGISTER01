namespace FVN_REGISTER.Core.Utils;

public static class DepartmentCodeParser
{
    public static bool TryParse(string? value, out int code)
        => int.TryParse(value?.Trim(), out code) && code > 0;

    public static int ParseRequired(string? value, string fieldName = "DeptCode")
    {
        if (!TryParse(value, out var code))
            throw new ArgumentException($"{fieldName} phải là mã phòng ban số hợp lệ.", fieldName);
        return code;
    }

    public static int? ParseNullable(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : TryParse(value, out var code) ? code : throw new ArgumentException("DeptCode phải là mã phòng ban số hợp lệ.", nameof(value));

    public static string? Format(int? value)
        => value?.ToString();
}
