using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FVN_REGISTER.Infrastructure.Models.Data.Converters;

/// <summary>
/// Department identity is INT in SQL Server (HRM NVMaBP / BPMa, every dbo.F03* *DeptCode column).
/// The application layer still carries department codes as text, so EF Core converts at the
/// storage boundary: text "57" is written as INT 57 and INT 57 is read back as text "57".
/// Applied by name to every mapped string property below, so no per-entity configuration is needed.
/// </summary>
internal static class DepartmentCodeStorage
{
    /// <summary>Property names that are INT in the database.</summary>
    public static readonly HashSet<string> ColumnNames = new(StringComparer.Ordinal)
    {
        "DeptCode", "ParentDeptCode", "SubDepartmentCode",
        "OldDeptCode", "NewDeptCode",
        "CurrentApproveForDeptCode", "SuggestedApproveForDeptCode",
        "ApproverDeptCode", "ApproveForDeptCode",
        "ResponsibleDeptCode", "RepairResponsibleDeptCode", "OperatingResponsibleDeptCode"
    };

    public static int ToInt(string? value)
        => int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var code)
            ? code
            : throw new FormatException($"Department code '{value}' is not a valid integer.");

    /// <summary>Blank text means "no department" for nullable columns.</summary>
    public static int? ToNullableInt(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : ToInt(value);

    public static string FromInt(int value)
        => value.ToString(CultureInfo.InvariantCulture);

    public static string? FromNullableInt(int? value)
        => value?.ToString(CultureInfo.InvariantCulture);

    private static readonly ValueConverter RequiredConverter =
        new ValueConverter<string, int>(v => ToInt(v), v => FromInt(v));

    private static readonly ValueConverter OptionalConverter =
        new ValueConverter<string?, int?>(v => ToNullableInt(v), v => FromNullableInt(v));

    /// <summary>
    /// Must run at the END of OnModelCreating so it sees properties created by every
    /// IEntityTypeConfiguration and every inline modelBuilder.Entity call.
    /// </summary>
    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType != typeof(string) || !ColumnNames.Contains(property.Name))
                    continue;

                // Length / nvarchar facets describe the old text column and do not apply to INT.
                property.SetMaxLength(null);
                property.SetColumnType(null);
                property.SetValueConverter(property.IsNullable ? OptionalConverter : RequiredConverter);
            }
        }
    }
}
