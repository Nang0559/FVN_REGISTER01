using System.Reflection;
using FVN_REGISTER.Core.Constants;
using Xunit;

namespace FVN_REGISTER.Infrastructure.Tests;

/// <summary>
/// Regression guard: FunctionCode is the legacy numeric identity of a permission. Two capabilities sharing one code
/// (PublicForm.View and Payroll.Approve both used 2810) make the SQL seed overwrite one with the other and the
/// Security Registry report a Conflict / retire the row.
/// </summary>
public sealed class SecurityFunctionCodeUniquenessTests
{
    private static IEnumerable<(string Owner, string Name, int Code)> AllCodes() =>
        new[] { typeof(SecurityFunctionCodes), typeof(ApprovalSecurityFunctionCodes) }
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(int))
                .Select(f => (t.Name, f.Name, (int)f.GetRawConstantValue()!)));

    [Fact]
    public void EveryFunctionCode_IsOwnedByExactlyOneCapability()
    {
        var duplicates = AllCodes()
            .GroupBy(x => x.Code)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(x => $"{x.Owner}.{x.Name}"))}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void PublicFormView_And_PayrollApprove_UseSeparateCodes()
    {
        Assert.Equal(2810, ApprovalSecurityFunctionCodes.PayrollApprove);
        Assert.Equal(2819, SecurityFunctionCodes.PublicFormView);
        Assert.NotEqual(SecurityFunctionCodes.PublicFormView, ApprovalSecurityFunctionCodes.PayrollApprove);
    }
}
