using Xunit;

namespace FVN_REGISTER.Infrastructure.Tests;

/// <summary>
/// A <c>[Fact]</c> that needs a real SQL Server database. When <c>FVN_REGISTER_SQL_CONNECTION</c> is not set the test is
/// reported as skipped (instead of failing <c>dotnet test</c>); set the variable to a disposable database to run it.
/// </summary>
public sealed class SqlIntegrationFactAttribute : FactAttribute
{
    public const string EnvironmentVariableName = "FVN_REGISTER_SQL_CONNECTION";

    public SqlIntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariableName)))
            Skip = $"Set {EnvironmentVariableName} to run this SQL Server integration test.";
    }
}
