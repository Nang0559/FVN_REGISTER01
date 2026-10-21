using System.Security.Cryptography;
using System.Text;
using FVN_REGISTER.AI.Api.Security;
using FVN_REGISTER.AI.Application.Configuration;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

[assembly: HostingStartup(typeof(FVN_REGISTER.AI.Api.DocumentDataEntryHostingStartup))]

namespace FVN_REGISTER.AI.Api;

/// <summary>Registers the reusable data-entry endpoints from outside Program.cs so the AI API remains modular.</summary>
public sealed class DocumentDataEntryHostingStartup : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services => services.AddTransient<IStartupFilter, DocumentDataEntryStartupFilter>());
    }
}

internal sealed class DocumentDataEntryStartupFilter(IConfiguration configuration) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            if (app is IEndpointRouteBuilder endpoints)
            {
                var group = endpoints.MapGroup("/api/ai/v1").AddEndpointFilter(new SignedAiEndpointFilter(configuration));
                group.MapDocumentDataEntryEndpoints();
            }
            next(app);
        };
    }
}

internal sealed class SignedAiEndpointFilter(IConfiguration configuration) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var configured = configuration[$"{AIPlatformOptions.SectionName}:ApiKey"];
        if (string.IsNullOrWhiteSpace(configured)) return Results.Problem("AI API key is not configured. Data-entry endpoints are disabled.", statusCode: 503);
        if (!http.Request.Headers.TryGetValue("X-AI-API-Key", out var supplied)) return Results.Unauthorized();
        var expectedBytes = Encoding.UTF8.GetBytes(configured); var suppliedBytes = Encoding.UTF8.GetBytes(supplied.ToString());
        if (expectedBytes.Length != suppliedBytes.Length || !CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes)) return Results.Unauthorized();

        var tenant = http.Request.Headers["X-Tenant-Code"].ToString().Trim(); var actor = http.Request.Headers["X-Actor-Id"].ToString().Trim();
        var timestamp = http.Request.Headers["X-AI-Timestamp"].ToString(); var nonce = http.Request.Headers["X-AI-Nonce"].ToString(); var signature = http.Request.Headers["X-AI-Signature"].ToString();
        var signingKey = configuration[$"{AIPlatformOptions.SectionName}:ContextSigningKey"];
        var options = configuration.GetSection(AIPlatformOptions.SectionName).Get<AIPlatformOptions>() ?? new AIPlatformOptions();
        var bindContent = options.RequireContentBoundSignatures; var bodySha256 = string.Empty;
        if (bindContent)
        {
            http.Request.EnableBuffering(); using var hash = SHA256.Create(); var digest = await hash.ComputeHashAsync(http.Request.Body, http.RequestAborted); bodySha256 = Convert.ToHexString(digest); http.Request.Body.Position = 0;
        }
        if (!RequestIdentitySignature.TryValidate(signingKey ?? string.Empty, http.Request.Method, http.Request.Path.Value ?? "/", tenant, actor, timestamp, nonce, signature, DateTimeOffset.UtcNow, out var identityFailure, bindContent ? http.Request.QueryString.Value ?? string.Empty : string.Empty, bodySha256))
        {
            if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32) return Results.Problem(identityFailure, statusCode: 503);
            return Results.Unauthorized();
        }
        var db = http.RequestServices.GetService<AiPlatformDbContext>(); if (db is null) return Results.Problem("AI SQL connection is required for replay protection.", statusCode: 503);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ai.AiRequestNonces(TenantCode, Nonce, ExpiresAtUtc) VALUES ({tenant}, {nonce}, {DateTimeOffset.UtcNow.AddMinutes(5)})", http.RequestAborted);
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 2601 or 2627) { return Results.Unauthorized(); }
        return await next(context);
    }
}
