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

    private static readonly Dictionary<string, (int R, int G, int B)> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aliceblue"] = (240, 248, 255),
        ["antiquewhite"] = (250, 235, 215),
        ["aqua"] = (0, 255, 255),
        ["aquamarine"] = (127, 255, 212),
        ["azure"] = (240, 255, 255),
        ["beige"] = (245, 245, 220),
        ["bisque"] = (255, 228, 196),
        ["black"] = (0, 0, 0),
        ["blanchedalmond"] = (255, 235, 205),
        ["blue"] = (0, 0, 255),
        ["blueviolet"] = (138, 43, 226),
        ["brown"] = (165, 42, 42),
        ["burlywood"] = (222, 184, 135),
        ["cadetblue"] = (95, 158, 160),
        ["chartreuse"] = (127, 255, 0),
        ["chocolate"] = (210, 105, 30),
        ["coral"] = (255, 127, 80),
        ["cornflowerblue"] = (100, 149, 237),
        ["cornsilk"] = (255, 248, 220),
        ["crimson"] = (220, 20, 60),
        ["cyan"] = (0, 255, 255),
        ["darkblue"] = (0, 0, 139),
        ["darkcyan"] = (0, 139, 139),
        ["darkgoldenrod"] = (184, 134, 11),
        ["darkgray"] = (169, 169, 169),
        ["darkgreen"] = (0, 100, 0),
        ["darkgrey"] = (169, 169, 169),
        ["darkkhaki"] = (189, 183, 107),
        ["darkmagenta"] = (139, 0, 139),
        ["darkolivegreen"] = (85, 107, 47),
        ["darkorange"] = (255, 140, 0),
        ["darkorchid"] = (153, 50, 204),
        ["darkred"] = (139, 0, 0),
        ["darksalmon"] = (233, 150, 122),
        ["darkseagreen"] = (143, 188, 143),
        ["darkslateblue"] = (72, 61, 139),
        ["darkslategray"] = (47, 79, 79),
        ["darkslategrey"] = (47, 79, 79),
        ["darkturquoise"] = (0, 206, 209),
        ["darkviolet"] = (148, 0, 211),
        ["deeppink"] = (255, 20, 147),
        ["deepskyblue"] = (0, 191, 255),
        ["dimgray"] = (105, 105, 105),
        ["dimgrey"] = (105, 105, 105),
        ["dodgerblue"] = (30, 144, 255),
        ["firebrick"] = (178, 34, 34),
        ["floralwhite"] = (255, 250, 240),
        ["forestgreen"] = (34, 139, 34),
        ["fuchsia"] = (255, 0, 255),
        ["gainsboro"] = (220, 220, 220),
        ["ghostwhite"] = (248, 248, 255),
        ["gold"] = (255, 215, 0),
        ["goldenrod"] = (218, 165, 32),
        ["gray"] = (128, 128, 128),
        ["green"] = (0, 128, 0),
        ["greenyellow"] = (173, 255, 47),
        ["grey"] = (128, 128, 128),
        ["honeydew"] = (240, 255, 240),
        ["hotpink"] = (255, 105, 180),
        ["indianred"] = (205, 92, 92),
        ["indigo"] = (75, 0, 130),
        ["ivory"] = (255, 255, 240),
        ["khaki"] = (240, 230, 140),
        ["lavender"] = (230, 230, 250),
        ["lavenderblush"] = (255, 240, 245),
        ["lawngreen"] = (124, 252, 0),
        ["lemonchiffon"] = (255, 250, 205),
        ["lightblue"] = (173, 216, 230),
        ["lightcoral"] = (240, 128, 128),
        ["lightcyan"] = (224, 255, 255),
        ["lightgoldenrodyellow"] = (250, 250, 210),
        ["lightgray"] = (211, 211, 211),
        ["lightgreen"] = (144, 238, 144),
        ["lightgrey"] = (211, 211, 211),
        ["lightpink"] = (255, 182, 193),
        ["lightsalmon"] = (255, 160, 122),
        ["lightseagreen"] = (32, 178, 170),
        ["lightskyblue"] = (135, 206, 250),
        ["lightslategray"] = (119, 136, 153),
        ["lightslategrey"] = (119, 136, 153),
        ["lightsteelblue"] = (176, 196, 222),
        ["lightyellow"] = (255, 255, 224),
        ["lime"] = (0, 255, 0),
        ["limegreen"] = (50, 205, 50),
        ["linen"] = (250, 240, 230),
        ["magenta"] = (255, 0, 255),
        ["maroon"] = (128, 0, 0),
        ["mediumaquamarine"] = (102, 205, 170),
        ["mediumblue"] = (0, 0, 205),
        ["mediumorchid"] = (186, 85, 211),
        ["mediumpurple"] = (147, 112, 219),
        ["mediumseagreen"] = (60, 179, 113),
        ["mediumslateblue"] = (123, 104, 238),
        ["mediumspringgreen"] = (0, 250, 154),
        ["mediumturquoise"] = (72, 209, 204),
        ["mediumvioletred"] = (199, 21, 133),
        ["midnightblue"] = (25, 25, 112),
        ["mintcream"] = (245, 255, 250),
        ["mistyrose"] = (255, 228, 225),
        ["moccasin"] = (255, 228, 181),
        ["navajowhite"] = (255, 222, 173),
        ["navy"] = (0, 0, 128),
        ["oldlace"] = (253, 245, 230),
        ["olive"] = (128, 128, 0),
        ["olivedrab"] = (107, 142, 35),
        ["orange"] = (255, 165, 0),
        ["orangered"] = (255, 69, 0),
        ["orchid"] = (218, 112, 214),
        ["palegoldenrod"] = (238, 232, 170),
        ["palegreen"] = (152, 251, 152),
        ["paleturquoise"] = (175, 238, 238),
        ["palevioletred"] = (219, 112, 147),
        ["papayawhip"] = (255, 239, 213),
        ["peachpuff"] = (255, 218, 185),
        ["peru"] = (205, 133, 63),
        ["pink"] = (255, 192, 203),
        ["plum"] = (221, 160, 221),
        ["powderblue"] = (176, 224, 230),
        ["purple"] = (128, 0, 128),
        ["rebeccapurple"] = (102, 51, 153),
        ["red"] = (255, 0, 0),
        ["rosybrown"] = (188, 143, 143),
        ["royalblue"] = (65, 105, 225),
        ["saddlebrown"] = (139, 69, 19),
        ["salmon"] = (250, 128, 114),
        ["sandybrown"] = (244, 164, 96),
        ["seagreen"] = (46, 139, 87),
        ["seashell"] = (255, 245, 238),
        ["sienna"] = (160, 82, 45),
        ["silver"] = (192, 192, 192),
        ["skyblue"] = (135, 206, 235),
        ["slateblue"] = (106, 90, 205),
        ["slategray"] = (112, 128, 144),
        ["slategrey"] = (112, 128, 144),
        ["snow"] = (255, 250, 250),
        ["springgreen"] = (0, 255, 127),
        ["steelblue"] = (70, 130, 180),
        ["tan"] = (210, 180, 140),
        ["teal"] = (0, 128, 128),
        ["thistle"] = (216, 191, 216),
        ["tomato"] = (255, 99, 71),
        ["turquoise"] = (64, 224, 208),
        ["violet"] = (238, 130, 238),
        ["wheat"] = (245, 222, 179),
        ["white"] = (255, 255, 255),
        ["whitesmoke"] = (245, 245, 245),
        ["yellow"] = (255, 255, 0),
        ["yellowgreen"] = (154, 205, 50),
    };

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
        // Tên màu CSS (đủ 148 tên, vd OrangeRed/Red/Crimson trong DM_Loainghile.LNLMauSac); tên lạ coi là nền sáng -> chữ đen.
        (r, g, b) = NamedColors.TryGetValue(v, out var rgb) ? rgb : (255, 255, 255);
        return true;
    }
}