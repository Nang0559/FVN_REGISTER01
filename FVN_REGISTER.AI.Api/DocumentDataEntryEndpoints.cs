using System.Text.Json;
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FVN_REGISTER.AI.Api;

public static class DocumentDataEntryEndpoints
{
    public static RouteGroupBuilder MapDocumentDataEntryEndpoints(this RouteGroupBuilder group)
    {
        var data = group.MapGroup("/data-entry");
        data.MapGet("/templates", async (HttpRequest req, IDocumentDataEntryTemplateService service, CancellationToken ct) =>
        {
            var tenant = Tenant(req); if (tenant is null) return Results.BadRequest(new { error = "Signed tenant context is required." });
            return Results.Ok(await service.ListAsync(tenant, ct));
        });
        data.MapPost("/templates", async (HttpRequest req, IDocumentDataEntryTemplateService service, IOptionsSnapshot<AIPlatform.Application.Configuration.AIPlatformOptions> options, CancellationToken ct) =>
        {
            var tenant = Tenant(req); var actor = Actor(req); if (tenant is null || actor is null) return Results.BadRequest(new { error = "Signed tenant and actor context are required." });
            if (!req.HasFormContentType) return Results.BadRequest(new { error = "multipart/form-data is required." });
            var form = await req.ReadFormAsync(ct); var file = form.Files.GetFile("file"); if (file is null || file.Length <= 0) return Results.BadRequest(new { error = "A non-empty XLSX template is required." });
            var max = (long)Math.Clamp(options.Value.MaximumFileSizeMb, 1, 250) * 1024 * 1024; if (file.Length > max) return Results.StatusCode(413);
            var definitionJson = form["definitionJson"].FirstOrDefault(); if (string.IsNullOrWhiteSpace(definitionJson)) return Results.BadRequest(new { error = "definitionJson is required." });
            DataEntryTemplateDefinition definition; try { definition = JsonSerializer.Deserialize<DataEntryTemplateDefinition>(definitionJson) ?? throw new JsonException(); } catch (JsonException) { return Results.BadRequest(new { error = "definitionJson is invalid." }); }
            await using var stream = file.OpenReadStream();
            try
            {
                var result = await service.CreateAsync(tenant, actor, form["templateCode"].FirstOrDefault() ?? "", form["displayName"].FirstOrDefault() ?? "", form["moduleCode"].FirstOrDefault() ?? "", form["schemaCode"].FirstOrDefault() ?? "", stream, definition, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (InvalidDataException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).DisableAntiforgery();
        data.MapPost("/runs", async (HttpRequest req, IDocumentDataEntryService service, IOptionsSnapshot<AIPlatform.Application.Configuration.AIPlatformOptions> options, CancellationToken ct) =>
        {
            var tenant = Tenant(req); var actor = Actor(req); if (tenant is null || actor is null) return Results.BadRequest(new { error = "Signed tenant and actor context are required." });
            if (!req.HasFormContentType) return Results.BadRequest(new { error = "multipart/form-data is required." });
            var form = await req.ReadFormAsync(ct); var file = form.Files.GetFile("file"); if (file is null || file.Length <= 0) return Results.BadRequest(new { error = "A non-empty document is required." });
            var max = (long)Math.Clamp(options.Value.MaximumFileSizeMb, 1, 250) * 1024 * 1024; if (file.Length > max) return Results.StatusCode(413);
            await using var stream = file.OpenReadStream();
            try { var receipt = await service.CreateRunAsync(tenant, actor, file.FileName, stream, form["templateCode"].FirstOrDefault() ?? "", ct); return Results.Accepted($"/api/ai/v1/data-entry/runs/{receipt.RunId:D}", receipt); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (InvalidDataException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).DisableAntiforgery();
        data.MapGet("/runs/{runId:guid}", async (Guid runId, HttpRequest req, IDocumentDataEntryService service, CancellationToken ct) =>
        {
            var tenant = Tenant(req); var actor = Actor(req); if (tenant is null || actor is null) return Results.BadRequest(new { error = "Signed tenant and actor context are required." });
            var result = await service.GetRunAsync(runId, tenant, actor, ct); return result is null ? Results.NotFound() : Results.Ok(result);
        });
        data.MapPost("/runs/{runId:guid}/confirm", async (Guid runId, ConfirmDataEntryRequest body, HttpRequest req, IDocumentDataEntryService service, CancellationToken ct) =>
        {
            var tenant = Tenant(req); var actor = Actor(req); if (tenant is null || actor is null) return Results.BadRequest(new { error = "Signed tenant and actor context are required." });
            try { return Results.Ok(await service.ConfirmAsync(runId, tenant, actor, body.RowsJson, body.Comment, ct)); }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
            catch (JsonException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });
        data.MapGet("/runs/{runId:guid}/export", async (Guid runId, HttpRequest req, IDocumentDataEntryService service, CancellationToken ct) =>
        {
            var tenant = Tenant(req); var actor = Actor(req); if (tenant is null || actor is null) return Results.BadRequest(new { error = "Signed tenant and actor context are required." });
            try
            {
                var file = await service.ExportAsync(runId, tenant, actor, ct); return file is null ? Results.NotFound() : Results.File(file.Content, file.ContentType, file.FileName);
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        });
        return group;
    }

    private static string? Tenant(HttpRequest request) { var value = request.Headers["X-Tenant-Code"].ToString().Trim(); return string.IsNullOrWhiteSpace(value) || value.Length > 64 ? null : value; }
    private static string? Actor(HttpRequest request) { var value = request.Headers["X-Actor-Id"].ToString().Trim(); return string.IsNullOrWhiteSpace(value) || value.Length > 128 ? null : value; }
}

public sealed record ConfirmDataEntryRequest(string RowsJson, string? Comment = null);
