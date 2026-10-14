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
    public void TryValidate_RejectsReplayedNonce()
    {
        var now = DateTimeOffset.UtcNow;
        var timestamp = now.ToUnixTimeSeconds().ToString();
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var signature = Sign(Method, Path, Tenant, Actor, timestamp, nonce);

        Assert.True(RequestIdentitySignature.TryValidate(Key, Method, Path, Tenant, Actor,
            timestamp, nonce, signature, now, out _));
        Assert.False(RequestIdentitySignature.TryValidate(Key, Method, Path, Tenant, Actor,
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

    private static string Sign(string method, string path, string tenant, string actor,
        string timestamp, string nonce)
    {
        var canonical = string.Join("\n", method, path, tenant, actor, timestamp, nonce);
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Key),
            Encoding.UTF8.GetBytes(canonical)));
    }
}
