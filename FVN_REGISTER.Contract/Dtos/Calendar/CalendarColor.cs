using System.Globalization;
using System.Text.RegularExpressions;

namespace FVN_REGISTER.Contract.Dtos.Calendar;

/// <summary>
/// Chuẩn hoá màu cấu hình trong HRM (DM_Loainghile.LNLMauSac) về màu CSS hợp lệ để vẽ ô lịch.
/// Chấp nhận: #RGB, #RRGGBB, RRGGBB, AARRGGBB, "R,G,B", "Color [A=255, R=255, G=0, B=255]", "Color [Magenta]",
/// tên màu CSS (Magenta, Blue...), số nguyên .NET ARGB (âm hoặc &gt; 0xFFFFFF) và số nguyên OLE/VB (BGR).
/// Giá trị không nhận ra được trả về null (ô giữ nền trắng) thay vì làm hỏng ô lịch.
/// </summary>
public static class CalendarColor
{
    private static readonly Regex Hex3Or6 = new("^#?([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$", RegexOptions.Compiled);
    private static readonly Regex Hex8 = new("^#?([0-9A-Fa-f]{8})$", RegexOptions.Compiled);
    private static readonly Regex Rgb = new(@"^(\d{1,3})\s*[,;]\s*(\d{1,3})\s*[,;]\s*(\d{1,3})$", RegexOptions.Compiled);
    private static readonly Regex NetArgb = new(@"A=(\d+),\s*R=(\d+),\s*G=(\d+),\s*B=(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NetNamed = new(@"^Color\s*\[([A-Za-z]{3,30})\]$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Integer = new(@"^-?\d{1,12}$", RegexOptions.Compiled);
    private static readonly Regex Named = new("^[A-Za-z]{3,30}$", RegexOptions.Compiled);

    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var v = raw.Trim();

        var m = NetArgb.Match(v);
        if (m.Success) return ToHex(int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value), int.Parse(m.Groups[4].Value));
        m = NetNamed.Match(v);
        if (m.Success) return m.Groups[1].Value;

        // AARRGGBB -> #RRGGBB. Chuỗi 8 chữ số thuần (vd 16711680) là số nguyên OLE, không phải hex.
        m = Hex8.Match(v);
        if (m.Success && (v.StartsWith('#') || v.Any(char.IsLetter))) return "#" + m.Groups[1].Value[2..].ToUpperInvariant();

        m = Hex3Or6.Match(v);
        if (m.Success)
        {
            var hex = m.Groups[1].Value;
            // Chuỗi toàn chữ số 6 ký tự (vd "255000") vẫn là hex; số nguyên dài hơn xử lý ở nhánh Integer.
            return "#" + hex.ToUpperInvariant();
        }

        m = Rgb.Match(v);
        if (m.Success) return ToHex(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));

        if (Integer.IsMatch(v) && long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
        {
            if (n < 0 || n > 0xFFFFFF)
            {
                var argb = unchecked((uint)n);
                return ToHex((int)((argb >> 16) & 0xFF), (int)((argb >> 8) & 0xFF), (int)(argb & 0xFF));
            }
            // OLE/VB6/Access: 0x00BBGGRR
            return ToHex((int)(n & 0xFF), (int)((n >> 8) & 0xFF), (int)((n >> 16) & 0xFF));
        }

        return Named.IsMatch(v) ? v : null;
    }

    /// <summary>Màu chữ (đen/trắng) đọc được trên nền <paramref name="background"/>; null nếu không có nền.</summary>
    public static string? ContrastText(string? background)
    {
        if (string.IsNullOrWhiteSpace(background) || !TryGetRgb(background, out var r, out var g, out var b)) return null;
        var luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255d;
        return luminance > 0.6 ? "#111111" : "#FFFFFF";
    }

    private static string ToHex(int r, int g, int b)
        => $"#{Math.Clamp(r, 0, 255):X2}{Math.Clamp(g, 0, 255):X2}{Math.Clamp(b, 0, 255):X2}";

    private static bool TryGetRgb(string css, out int r, out int g, out int b)
    {
        r = g = b = 0;
        var v = css.Trim();
        if (v.StartsWith('#'))
        {
            v = v[1..];
            if (v.Length == 3) v = string.Concat(v.Select(c => new string(c, 2)));
            if (v.Length == 6
                && int.TryParse(v[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r)
                && int.TryParse(v[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g)
                && int.TryParse(v[4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b))
                return true;
            return false;
        }
        // Tên màu CSS phổ biến trong cấu hình HRM; tên lạ coi là nền sáng -> chữ đen.
        (r, g, b) = v.ToLowerInvariant() switch
        {
            "magenta" or "fuchsia" => (255, 0, 255),
            "blue" => (0, 0, 255),
            "yellow" => (255, 255, 0),
            "lime" => (0, 255, 0),
            "green" => (0, 128, 0),
            "red" => (255, 0, 0),
            "cyan" or "aqua" => (0, 255, 255),
            "orange" => (255, 165, 0),
            "black" => (0, 0, 0),
            "navy" => (0, 0, 128),
            "purple" => (128, 0, 128),
            _ => (255, 255, 255)
        };
        return true;
    }
}
