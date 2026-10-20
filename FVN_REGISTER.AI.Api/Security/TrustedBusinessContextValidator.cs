using System.Text.Json;

namespace FVN_REGISTER.AI.Api.Security;

/// <summary>
/// Validates the minimized FVN_REGISTER context envelope before it can influence
/// answer phrasing. This metadata is never an authorization grant.
/// </summary>
public static class TrustedBusinessContextValidator
{
    private const int MaximumJsonLength = 32_768;
    private const int MaximumCodes = 500;
    private const int MaximumCodeLength = 160;

    public static bool TryValidate(JsonElement context, string tenant, string actor, out string error)
    {
        error = "Business context is incomplete or exceeds supported limits.";
        if (context.ValueKind != JsonValueKind.Object ||
            string.IsNullOrWhiteSpace(tenant) || tenant.Length > 64 ||
            string.IsNullOrWhiteSpace(actor) || actor.Length > 128)
            return false;

        var raw = context.GetRawText();
        if (raw.Length > MaximumJsonLength)
        {
            error = "Business context exceeds the 32768-character limit.";
            return false;
        }

        if (!TryRequiredString(context, "tenantCode", 64, out var contextTenant) ||
            !TryRequiredString(context, "actorId", 128, out var contextActor) ||
            !TryRequiredString(context, "contextVersion", 128, out _))
            return false;

        if (!string.Equals(contextTenant, tenant, StringComparison.Ordinal) ||
            !string.Equals(contextActor, actor, StringComparison.Ordinal))
        {
            error = "Business context identity does not match the signed request identity.";
            return false;
        }

        if (!TryCodeArray(context, "roleCodes") ||
            !TryCodeArray(context, "permissionCodes") ||
            !TryCodeArray(context, "functionScopeCodes") ||
            !TryCodeArray(context, "managedScopeCodes"))
            return false;

        if (!TryOptionalString(context, "departmentCode", 128) ||
            !TryOptionalString(context, "positionCode", 128))
            return false;

        error = string.Empty;
        return true;
    }

    private static bool TryRequiredString(JsonElement source, string name, int maximumLength, out string value)
    {
        value = string.Empty;
        if (!source.TryGetProperty(name, out var item) || item.ValueKind != JsonValueKind.String)
            return false;
        value = item.GetString()?.Trim() ?? string.Empty;
        return value.Length > 0 && value.Length <= maximumLength;
    }

    private static bool TryOptionalString(JsonElement source, string name, int maximumLength)
    {
        if (!source.TryGetProperty(name, out var item) || item.ValueKind == JsonValueKind.Null)
            return true;
        return item.ValueKind == JsonValueKind.String &&
            (item.GetString()?.Length ?? 0) <= maximumLength;
    }

    private static bool TryCodeArray(JsonElement source, string name)
    {
        if (!source.TryGetProperty(name, out var values) ||
            values.ValueKind != JsonValueKind.Array || values.GetArrayLength() > MaximumCodes)
            return false;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in values.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                return false;
            var code = item.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(code) || code.Length > MaximumCodeLength || !seen.Add(code))
                return false;
        }
        return true;
    }
}
