using System.Reflection;

namespace FVN_REGISTER.Infrastructure.Services.Excel;

/// <summary>
/// Central guard/documentation point for the Shared Excel Platform.
/// Business services must depend on IExcelPlatform; this implementation is resolved by DI only.
/// </summary>
internal static class ExcelPlatformDependencyAudit
{
    public static readonly Type ImplementationType = typeof(ExcelPlatform);
    public static readonly Assembly ImplementationAssembly = typeof(ExcelPlatform).Assembly;
}
