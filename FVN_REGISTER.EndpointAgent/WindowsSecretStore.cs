using System.Security.Cryptography;
using System.Text;

namespace FVN_REGISTER.EndpointAgent;

public static class WindowsSecretStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FVN_REGISTER.EndpointAgent.v1");

    public static string Protect(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret)) throw new ArgumentException("Secret is required.", nameof(secret));
        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret.Trim()), Entropy, DataProtectionScope.LocalMachine);
        return Convert.ToBase64String(protectedBytes);
    }

    public static string Unprotect(string protectedSecret)
    {
        if (string.IsNullOrWhiteSpace(protectedSecret)) throw new ArgumentException("Protected secret is required.", nameof(protectedSecret));
        var bytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedSecret), Entropy, DataProtectionScope.LocalMachine);
        return Encoding.UTF8.GetString(bytes);
    }
}
