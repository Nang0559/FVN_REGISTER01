namespace FVN_REGISTER.Contract.Help;

/// <summary>
/// Single source of truth that links a help feature code (see <c>HelpRegistry</c>) to the sections of the Markdown user guide.
/// It lives in Contract so that both the Blazor guide page (<c>/huong-dan</c>) and the guide assistant on the API use the same map.
/// An id like "7.3" is the heading "### 7.3. ..."; an id starting with "~" is a heading matched by its exact text.
/// </summary>
public static class GuideSectionMap
{
    /// <summary>The main guide that <c>/huong-dan</c> renders and that the section ids below refer to.</summary>
    public const string MainGuideFile = "04_USER_GUIDE.md";

    public static readonly IReadOnlyDictionary<string, string[]> Map = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["HOME_WORKSPACE"] = new[] { "1.1" },
        ["WORK_CALENDAR"] = new[] { "1.2" },
        ["APPROVAL_INBOX"] = new[] { "1.3", "3.1" },
        ["LEAVE"] = new[] { "2.1" },
        ["OT"] = new[] { "2.2" },
        ["TRIP"] = new[] { "2.3" },
        ["EXECUTION"] = new[] { "3.2" },
        ["EQUIPMENT_WORKSPACE"] = new[] { "4.1", "4.2" },
        ["EQUIPMENT_CHECKLIST"] = new[] { "4.1" },
        ["EQUIPMENT_REPAIR"] = new[] { "4.1" },
        ["EQUIPMENT_HANDOVER"] = new[] { "4.1" },
        ["EQUIPMENT_IMPORT"] = new[] { "4.2" },
        ["EQUIPMENT_SCHEMA"] = new[] { "4.2" },
        ["PUBLIC_INFORMATION_CMS"] = new[] { "5.2" },
        ["PUBLIC_FORMS"] = new[] { "5.1", "5.2" },
        ["SECURITY_CENTER"] = new[] { "6.1" },
        ["ENDPOINT_MANAGEMENT"] = new[] { "6.2" },
        ["ADMIN_ORGANIZATION"] = new[] { "7.1" },
        ["ADMIN_POLICIES"] = new[] { "7.2", "7.3" },
        ["HRM_ATTENDANCE_CALCULATION"] = new[] { "7.3" },
        ["ADMIN_ATTENDANCE_SYMBOL_RULES"] = new[] { "7.3" },
        ["ADMIN_HRM_SYNC"] = new[] { "7.4" },
        ["ADMIN_HRM_ATTENDANCE_JOBS"] = new[] { "7.4" },
        ["ADMIN_SYSTEM"] = new[] { "7.4" },
        ["PAYROLL_TABLE"] = new[] { "~Nghiệp vụ, chấm công và báo cáo" },
        ["REPORTS"] = new[] { "~Nghiệp vụ, chấm công và báo cáo" },
        ["HISTORY"] = new[] { "~Nghiệp vụ, chấm công và báo cáo" },
    };

    private static readonly IReadOnlyDictionary<string, string[]> Inverse = Map
        .SelectMany(pair => pair.Value.Select(section => (Section: section, Feature: pair.Key)))
        .GroupBy(x => x.Section, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.Select(x => x.Feature).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.OrdinalIgnoreCase);

    /// <summary>The guide sections that document <paramref name="featureCode"/>; empty when the guide has none for it.</summary>
    public static string[] SectionsFor(string? featureCode) =>
        featureCode is not null && Map.TryGetValue(featureCode, out var ids) ? ids : Array.Empty<string>();

    /// <summary>The feature codes whose guide includes <paramref name="sectionId"/>; empty when the section is not mapped.</summary>
    public static string[] FeaturesFor(string? sectionId) =>
        sectionId is not null && Inverse.TryGetValue(sectionId, out var features) ? features : Array.Empty<string>();
}
