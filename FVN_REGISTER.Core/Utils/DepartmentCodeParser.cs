namespace FVN_REGISTER.Core.Utils;

public static class DepartmentCodeParser
{
    public static bool TryParse(string? value, out string code)
    {
        code = value?.Trim() ?? string.Empty;
        return code.Length > 0;
    }

    // Legacy numeric overload kept only for non-persisted compatibility; canonical DeptCode remains string.
    public static bool TryParse(string? value, out int code)
        => int.TryParse(value?.Trim(), out code) && code > 0;

    public static string ParseRequired(string? value, string fieldName = "DeptCode")
    {
        var code = value?.Trim();
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException($"{fieldName} là bắt buộc.", fieldName);
        return code;
    }

    public static string? ParseNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string? Format(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
