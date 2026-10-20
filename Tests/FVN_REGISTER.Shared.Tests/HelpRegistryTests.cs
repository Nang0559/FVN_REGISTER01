using System.Text.RegularExpressions;
using FVN_REGISTER.Shared.Services.Help;
using FVN_REGISTER.Shared.Services.Language;
using FVN_REGISTER.Contract.Dtos.Security;
using FVN_REGISTER.Core.Constants;
using Xunit;

namespace FVN_REGISTER.Shared.Tests;

/// <summary>
/// Guards the contextual help ("?"): every routable page must resolve to its own help text, and every help code must
/// have all of its texts in Vietnamese and Japanese. Adding a page without a help rule fails the build.
/// </summary>
[Collection("LocalizationStore")]
public sealed class HelpRegistryTests
{
    private static readonly Regex PageDirective = new(@"^\s*@page\s+""([^""]+)""", RegexOptions.Compiled | RegexOptions.Multiline);

    [Theory]
    [InlineData("/", "HOME_WORKSPACE")]
    [InlineData("/hrm/attendance-calculation", "HRM_ATTENDANCE_CALCULATION")]
    [InlineData("https://host/hrm/attendance-calculation?x=1#top", "HRM_ATTENDANCE_CALCULATION")]
    [InlineData("hrm/attendance-calculation", "HRM_ATTENDANCE_CALCULATION")]
    [InlineData("/admin/work-calendar", "ADMIN_WORK_CALENDAR")]
    [InlineData("/admin/attendance-symbol-rules", "ADMIN_ATTENDANCE_SYMBOL_RULES")]
    [InlineData("/admin/ot-registrars", "ADMIN_OT_REGISTRARS")]
    [InlineData("/admin/security/function-registry", "SECURITY_CENTER")]
    [InlineData("/approvals", "APPROVAL_INBOX")]
    [InlineData("/work/inbox", "APPROVAL_INBOX")]
    [InlineData("/equipment/inspection/12", "EQUIPMENT_CHECKLIST")]
    [InlineData("/equipment/repair/5", "EQUIPMENT_REPAIR")]
    [InlineData("/equipment/7/endpoint-agent", "EQUIPMENT_WORKSPACE")]
    [InlineData("/history/leave", "HISTORY")]
    [InlineData("/OT/Create", "OT")]
    public void Route_Resolves_ToItsOwnFeature(string route, string expected) =>
        Assert.Equal(expected, HelpRegistry.Resolve(route));

    [Theory]
    [InlineData("/login")]
    [InlineData("/huong-dan")]
    [InlineData("/public")]
    [InlineData("/no/such/page")]
    public void UnknownRoute_HasNoHelp_AndDoesNotFallBackToHome(string route) =>
        Assert.Null(HelpRegistry.Resolve(route));

    [Fact]
    public void EveryFeatureCode_HasAllTexts_InBothLanguages()
    {
        var problems = new List<string>();
        foreach (var code in HelpRegistry.AllFeatureCodes)
            foreach (var language in new[] { LanguageCode.Vi, LanguageCode.Ja })
                foreach (var part in HelpRegistry.TextParts)
                {
                    var key = $"help.{code}.{part}";
                    var all = LocalizationStore.GetAll(language);
                    if (!all.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
                        problems.Add($"{language}: missing '{key}'");
                }
        Assert.Empty(problems);
    }

    [Fact]
    public void EveryPage_HasAHelpRule_OrIsListedAsWithoutHelp()
    {
        var root = FindRepositoryRoot();
        var sourceRoots = new[] { "FVN_REGISTER.Shared", "FVN_REGISTER.Web", "FVN_REGISTER.UI" }
            .Select(name => Path.Combine(root, name))
            .Where(Directory.Exists);

        var problems = new List<string>();
        foreach (var file in sourceRoots.SelectMany(dir => Directory.EnumerateFiles(dir, "*.razor", SearchOption.AllDirectories)))
        {
            var sep = Path.DirectorySeparatorChar;
            if (file.Contains($"{sep}bin{sep}") || file.Contains($"{sep}obj{sep}")) continue;

            foreach (Match m in PageDirective.Matches(File.ReadAllText(file)))
            {
                var route = m.Groups[1].Value;
                if (HelpRegistry.Resolve(route) is not null) continue;

                var first = HelpRegistry.Normalize(route).FirstOrDefault() ?? string.Empty;
                if (!HelpRegistry.RoutesWithoutHelp.Contains(first, StringComparer.OrdinalIgnoreCase))
                    problems.Add($"{Path.GetRelativePath(root, file)}: @page \"{route}\" has no help rule in HelpRegistry");
            }
        }
        Assert.Empty(problems.Distinct().OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void HomeText_IsOnlyUsedForTheHomePage()
    {
        var offenders = new[] { "/hrm/attendance-calculation", "/admin/work-calendar", "/payroll/table", "/execution", "/profile" }
            .Where(route => HelpRegistry.Resolve(route) == HelpRegistry.Home)
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void HelpAccessPolicy_UsesEffectiveFunctionGrants_NotSingleLegacyRole()
    {
        var approver = new PermissionSnapshotDto { RoleCodes = new List<int> { UserPermissionCodes.User } };
        approver.FunctionCodes.Add(SecurityFunctionCodes.OTApprove);
        Assert.True(HelpAccessPolicy.CanView(approver, "APPROVAL_INBOX"));
        Assert.False(HelpAccessPolicy.CanView(approver, "SECURITY_CENTER"));

        var hr = new PermissionSnapshotDto { RoleCodes = new List<int> { UserPermissionCodes.HR } };
        hr.FunctionCodes.Add(SecurityFunctionCodes.AttendanceCalculate);
        Assert.True(HelpAccessPolicy.CanView(hr, "HRM_ATTENDANCE_CALCULATION"));
        Assert.False(HelpAccessPolicy.CanView(hr, "ADMIN_WORK_CALENDAR"));
    }

    [Fact]
    public void HelpAccessPolicy_DeniesMissingSnapshotsAndUnknownFeatures()
    {
        Assert.False(HelpAccessPolicy.CanView(null, "APPROVAL_INBOX"));
        Assert.False(HelpAccessPolicy.CanView(new PermissionSnapshotDto(), "NOT_A_FEATURE"));
    }
    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "FVN_REGISTER.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("FVN_REGISTER.sln not found above " + AppContext.BaseDirectory);
    }
}
