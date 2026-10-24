using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FVN_REGISTER.AI.Api.Security;

/// <summary>
/// Validates signed service-to-service user context. The HMAC secret must be shared
/// only by the trusted FVN_REGISTER backend and this AI API.
/// </summary>
public static class RequestIdentitySignature
{
    private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromMinutes(5);

    public static bool TryValidate(
        string signingKey,
        string method,
        string path,
        string tenantCode,
        string actorId,
        string timestamp,
        string nonce,
        string signature,
        DateTimeOffset nowUtc,
        out string failure,
        string queryString = "",
        string bodySha256 = "")
    {
        failure = "Signed service identity is invalid.";
        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
        {
            failure = "AI context signing key is not configured (minimum 32 characters).";
            return false;
        }

        if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var unixSeconds))
            return false;

        DateTimeOffset signedAt;
        try { signedAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds); }
        catch (ArgumentOutOfRangeException) { return false; }

        if ((nowUtc - signedAt).Duration() > AllowedClockSkew)
        {
            failure = "Signed service identity has expired.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(tenantCode) || tenantCode.Length > 64 ||
            string.IsNullOrWhiteSpace(actorId) || actorId.Length > 128 ||
            string.IsNullOrWhiteSpace(nonce) || nonce.Length is < 16 or > 128 ||
            string.IsNullOrWhiteSpace(signature))
            return false;

        var canonical = string.IsNullOrEmpty(bodySha256)
            ? string.Join("\n", method.ToUpperInvariant(), path, tenantCode, actorId, timestamp, nonce)
            : string.Join("\n", method.ToUpperInvariant(), path + queryString, tenantCode, actorId, timestamp, nonce, bodySha256.ToUpperInvariant());
        var key = Encoding.UTF8.GetBytes(signingKey);
        var expected = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(canonical));
        byte[] supplied;
        try { supplied = Convert.FromHexString(signature); }
        catch (FormatException) { return false; }
        if (supplied.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(expected, supplied))
            return false;

        // Replay protection is persisted by the API in SQL so it works across replicas.
        return true;
    }
}
