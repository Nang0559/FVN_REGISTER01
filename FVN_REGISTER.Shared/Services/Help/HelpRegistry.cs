using FVN_REGISTER.Shared.Services.Language;

namespace FVN_REGISTER.Shared.Services.Help;

/// <summary>
/// Single source of truth that maps a page route to the feature code of its help text
/// (<c>help.{FEATURE}.title|summary|flow|steps|example|security</c> in <c>Localization/*.help.json</c>).
/// Used by both the contextual "?" drawer (<c>FeatureHelp</c>) and the detailed guide page (<c>FeatureGuide</c>).
///
/// Rules:
///  - the most specific matching rule wins (more segments first, then an exact rule before a prefix rule);
///  - a route with no rule returns <c>null</c>: the UI must say "no guide yet" and must NOT fall back to the home text;
///  - <c>HelpRegistryTests</c> fails when a <c>@page</c> route has no rule (or is not listed as intentionally without help),
///    or when a feature code is missing its VI/JA text.
/// </summary>
public static class HelpRegistry
{
    public const string Home = "HOME_WORKSPACE";

    /// <summary>The six text parts every feature code must provide, in both languages.</summary>
    public static readonly string[] TextParts = { "title", "summary", "flow", "steps", "example", "security" };

    private sealed record Rule(string[] Pattern, string Feature, bool Exact);

    // Pattern segments are compared case-insensitively; "*" (or a "{param}" segment) matches any single segment.
    // Exact = false means "this route and everything below it".
    private static readonly Rule[] Rules =
    {
        Exact("", Home),

        // Equipment
        Prefix("equipment/checklist", "EQUIPMENT_CHECKLIST"),
        Prefix("equipment/inspection", "EQUIPMENT_CHECKLIST"),
        Prefix("equipment/inspection-management", "EQUIPMENT_CHECKLIST"),
        Prefix("equipment/schema", "EQUIPMENT_SCHEMA"),
        Prefix("equipment/schema-management", "EQUIPMENT_SCHEMA"),
        Prefix("equipment/import", "EQUIPMENT_IMPORT"),
        Prefix("equipment/repair", "EQUIPMENT_REPAIR"),
        Prefix("equipment/handover", "EQUIPMENT_HANDOVER"),
        Prefix("equipment", "EQUIPMENT_WORKSPACE"),

        // Endpoint / software inventory
        Prefix("endpoint-inventory", "ENDPOINT_MANAGEMENT"),
        Prefix("endpoint-governance", "ENDPOINT_MANAGEMENT"),
        Prefix("endpoint-credentials", "ENDPOINT_MANAGEMENT"),

        // Public information and public forms
        Prefix("public-information", "PUBLIC_INFORMATION_CMS"),
        Prefix("thong-tin-cong-khai", "PUBLIC_INFORMATION_CMS"),
        Prefix("admin/public-information", "PUBLIC_INFORMATION_CMS"),
        Prefix("public-forms", "PUBLIC_FORMS"),
        Prefix("admin/public-forms", "PUBLIC_FORMS"),

        // Work inbox / approvals
        Prefix("approval", "APPROVAL_INBOX"),
        Prefix("approvals", "APPROVAL_INBOX"),
        Prefix("inbox", "APPROVAL_INBOX"),
        Prefix("work/inbox", "APPROVAL_INBOX"),

        // Employee self-service modules
        Prefix("calendar", "WORK_CALENDAR"),
        Prefix("work-calendar", "WORK_CALENDAR"),
        Prefix("leave", "LEAVE"),
        Prefix("ot", "OT"),
        Prefix("trip", "TRIP"),
        Prefix("history", "HISTORY"),
        Prefix("reports", "REPORTS"),
        Prefix("report", "REPORTS"),
        Prefix("execution", "EXECUTION"),
        Prefix("payroll", "PAYROLL_TABLE"),
        Prefix("profile", "PROFILE"),
        Prefix("change-password", "PROFILE"),

        // HRM
        Prefix("hrm/attendance-calculation", "HRM_ATTENDANCE_CALCULATION"),

        // Security
        Prefix("security", "SECURITY_CENTER"),
        Prefix("admin/security", "SECURITY_CENTER"),
        Prefix("admin/sessions", "SECURITY_CENTER"),

        // Administration (one code per group of related screens)
        Prefix("admin/work-calendar", "ADMIN_WORK_CALENDAR"),
        Prefix("admin/attendance-symbol-rules", "ADMIN_ATTENDANCE_SYMBOL_RULES"),
        Prefix("admin/hrm-sync", "ADMIN_HRM_SYNC"),
        Prefix("admin/hrm-sync-review", "ADMIN_HRM_SYNC"),
        Prefix("admin/hrm-attendance-calculation-jobs", "ADMIN_HRM_ATTENDANCE_JOBS"),
        Prefix("admin/hrm-attendance-calculation-monitor", "ADMIN_HRM_ATTENDANCE_JOBS"),
        Prefix("admin/ot-registrars", "ADMIN_OT_REGISTRARS"),
        Prefix("admin/departments", "ADMIN_ORGANIZATION"),
        Prefix("admin/employees", "ADMIN_ORGANIZATION"),
        Prefix("admin/managers", "ADMIN_ORGANIZATION"),
        Prefix("admin/approvers", "ADMIN_ORGANIZATION"),
        Prefix("admin/approval-policies", "ADMIN_POLICIES"),
        Prefix("admin/execution-policies", "ADMIN_POLICIES"),
        Prefix("admin/ot-limit-rules", "ADMIN_POLICIES"),
        Prefix("admin/hrm-role-rules", "ADMIN_POLICIES"),
        Prefix("admin/leave-types", "ADMIN_POLICIES"),
        Prefix("admin/email-profiles", "ADMIN_SYSTEM"),
        Prefix("admin/email-queue", "ADMIN_SYSTEM"),
        Prefix("admin/email-templates", "ADMIN_SYSTEM"),
        Prefix("admin/background-jobs", "ADMIN_SYSTEM"),
        Prefix("admin/system-update", "ADMIN_SYSTEM"),
        Prefix("admin/localization", "ADMIN_SYSTEM"),
    };

    /// <summary>Routes that intentionally have no help text (login, the guide page itself, the public landing page).</summary>
    public static readonly IReadOnlyList<string> RoutesWithoutHelp = new[] { "login", "huong-dan", "public" };

    /// <summary>The user guide that <c>/huong-dan</c> renders (copied next to the app by FVN_REGISTER.Web.csproj).</summary>
    public const string GuideFile = "04_USER_GUIDE.md";

    // Feature code -> sections of GuideFile. An id like "7.3" is the heading "### 7.3. ..."; an id starting with "~" is a
    // heading matched by its exact text (used for the appendix). Codes without an entry (or whose section is missing from
    // the file) fall back to the short overview stored in Localization/*.help.json.
    // To document a new screen: add a "### 7.5. ..." section to the .md and list "7.5" here (GuideMarkdownTests checks the ids).
    private static readonly IReadOnlyDictionary<string, string[]> GuideMap = new Dictionary<string, string[]>(StringComparer.Ordinal)
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

    public static IReadOnlyDictionary<string, string[]> GuideSections => GuideMap;

    /// <summary>The guide sections that document <paramref name="featureCode"/>; empty when the guide has none for it.</summary>
    public static string[] GuideSectionsFor(string? featureCode) =>
        featureCode is not null && GuideMap.TryGetValue(featureCode, out var ids) ? ids : Array.Empty<string>();


    /// <summary>Every feature code that appears in a rule (plus the home code).</summary>
    public static IReadOnlyCollection<string> AllFeatureCodes { get; } =
        Rules.Select(r => r.Feature).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    /// <summary>
    /// Returns the feature code for <paramref name="route"/> (absolute or base-relative, query/hash allowed),
    /// or <c>null</c> when the route has no help rule.
    /// </summary>
    public static string? Resolve(string? route)
    {
        var segments = Normalize(route);
        Rule? best = null;
        foreach (var rule in Rules)
        {
            if (!Matches(rule, segments)) continue;
            if (best is null
                || rule.Pattern.Length > best.Pattern.Length
                || (rule.Pattern.Length == best.Pattern.Length && rule.Exact && !best.Exact))
                best = rule;
        }
        return best?.Feature;
    }

    /// <summary>True when the feature code has all six texts in the given language (a Japanese gap falls back to Vietnamese).</summary>
    public static bool HasContent(LanguageCode language, string? featureCode)
    {
        if (string.IsNullOrWhiteSpace(featureCode)) return false;
        foreach (var part in TextParts)
            if (!LocalizationStore.TryGet(language, $"help.{featureCode}.{part}", out var value) || string.IsNullOrWhiteSpace(value))
                return false;
        return true;
    }

    /// <summary>Normalises "https://host/app/hrm/x?y=1#z" or "/hrm/x" to its path segments.</summary>
    public static string[] Normalize(string? route)
    {
        var text = (route ?? string.Empty).Trim();
        var schemeEnd = text.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd >= 0)
        {
            var pathStart = text.IndexOf('/', schemeEnd + 3);
            text = pathStart < 0 ? string.Empty : text[pathStart..];
        }
        var clean = text.Split('?', '#')[0].Trim('/');
        return clean.Length == 0
            ? Array.Empty<string>()
            : clean.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool Matches(Rule rule, string[] segments)
    {
        if (rule.Exact ? segments.Length != rule.Pattern.Length : segments.Length < rule.Pattern.Length)
            return false;
        for (var i = 0; i < rule.Pattern.Length; i++)
        {
            var p = rule.Pattern[i];
            if (p == "*" || (p.StartsWith('{') && p.EndsWith('}'))) continue;
            if (!string.Equals(segments[i], p, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    private static Rule Exact(string pattern, string feature) => new(Split(pattern), feature, true);
    private static Rule Prefix(string pattern, string feature) => new(Split(pattern), feature, false);
    private static string[] Split(string pattern) =>
        pattern.Length == 0 ? Array.Empty<string>() : pattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
}
