using System.Security.Cryptography;
using System.Text;
using FVN_REGISTER.AI.Api.Security;

namespace FVN_REGISTER.AI.Tests;

public sealed class RequestIdentitySignatureTests
{
    private const string Key = "test-only-signing-key-with-at-least-32-characters";
    private const string Method = "POST";
    private const string Path = "/api/ai/v1/documents";
    private const string Tenant = "FCCVN";
    private const string Actor = "E0001";

    [Fact]
    public void TryValidate_AcceptsValidSignature()
    {
        var now = DateTimeOffset.UtcNow;
        var timestamp = now.ToUnixTimeSeconds().ToString();
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var signature = Sign(Method, Path, Tenant, Actor, timestamp, nonce);

        var valid = RequestIdentitySignature.TryValidate(Key, Method, Path, Tenant, Actor,
            timestamp, nonce, signature, now, out _);

        Assert.True(valid);
    }

    [Fact]
    public void TryValidate_ValidatesSignatureButLeavesReplayPersistenceToGatewayMiddleware()
    {
        var now = DateTimeOffset.UtcNow;
        var timestamp = now.ToUnixTimeSeconds().ToString();
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var signature = Sign(Method, Path, Tenant, Actor, timestamp, nonce);

        Assert.True(RequestIdentitySignature.TryValidate(Key, Method, Path, Tenant, Actor,
            timestamp, nonce, signature, now, out _));
        // HMAC validation is intentionally stateless. The API endpoint filter persists
        // nonces in SQL and rejects duplicates across requests/replicas.
        Assert.True(RequestIdentitySignature.TryValidate(Key, Method, Path, Tenant, Actor,
            timestamp, nonce, signature, now, out _));
    }

    [Fact]
    public void TryValidate_RejectsExpiredTimestamp()
    {
        var now = DateTimeOffset.UtcNow;
        var timestamp = now.AddMinutes(-10).ToUnixTimeSeconds().ToString();
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var signature = Sign(Method, Path, Tenant, Actor, timestamp, nonce);

        Assert.False(RequestIdentitySignature.TryValidate(Key, Method, Path, Tenant, Actor,
            timestamp, nonce, signature, now, out _));
    }

    [Fact]
    public void TryValidate_RejectsTamperedActor()
    {
        var now = DateTimeOffset.UtcNow;
        var timestamp = now.ToUnixTimeSeconds().ToString();
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var signature = Sign(Method, Path, Tenant, Actor, timestamp, nonce);

        Assert.False(RequestIdentitySignature.TryValidate(Key, Method, Path, Tenant, "E9999",
            timestamp, nonce, signature, now, out _));
    }

    [Fact]
    public void TryValidate_AcceptsExactContentBoundGatewayRequest()
    {
        var now = DateTimeOffset.UtcNow;
        var timestamp = now.ToUnixTimeSeconds().ToString();
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var bodyHash = Convert.ToHexString(SHA256.HashData(Array.Empty<byte>()));
        var canonical = string.Join("\n", "GET", "/api/ai/v1/business-context/me",
            Tenant, Actor, timestamp, nonce, bodyHash);
        var signature = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(Key), Encoding.UTF8.GetBytes(canonical)));

        Assert.True(RequestIdentitySignature.TryValidate(Key, "GET",
            "/api/ai/v1/business-context/me", Tenant, Actor, timestamp, nonce,
            signature, now, out _, string.Empty, bodyHash));
    }

    [Fact]
    public void TryValidate_RejectsContentBoundActorAndBodyTampering()
    {
        var now = DateTimeOffset.UtcNow;
        var timestamp = now.ToUnixTimeSeconds().ToString();
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var bodyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("{\\"value\\":1}")));
        var canonical = string.Join("\n", "POST", "/api/ai/v1/business-context/snapshot",
            Tenant, Actor, timestamp, nonce, bodyHash);
        var signature = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(Key), Encoding.UTF8.GetBytes(canonical)));

        Assert.False(RequestIdentitySignature.TryValidate(Key, "POST",
            "/api/ai/v1/business-context/snapshot", Tenant, "E9999", timestamp, nonce,
            signature, now, out _, string.Empty, bodyHash));

        var tamperedBodyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("{\\"value\\":2}")));
        Assert.False(RequestIdentitySignature.TryValidate(Key, "POST",
            "/api/ai/v1/business-context/snapshot", Tenant, Actor, timestamp, nonce,
            signature, now, out _, string.Empty, tamperedBodyHash));
    }

    private static string Sign(string method, string path, string tenant, string actor,
        string timestamp, string nonce)
    {
        var canonical = string.Join("\n", method, path, tenant, actor, timestamp, nonce);
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Key),
            Encoding.UTF8.GetBytes(canonical)));
    }
}
