using System.Security.Cryptography;
using System.Text;
using FVN_REGISTER.AI.Api.Security;
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Application.Configuration;
using FVN_REGISTER.AI.Infrastructure;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http.Features;
var builder=WebApplication.CreateBuilder(args);
var settings=builder.Configuration.GetSection(AIPlatformOptions.SectionName).Get<AIPlatformOptions>()??new AIPlatformOptions();
var maximumUploadBytes=(long)Math.Clamp(settings.MaximumFileSizeMb,1,250)*1024*1024;
builder.WebHost.ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=maximumUploadBytes+1024*1024);
builder.Services.Configure<FormOptions>(o=>o.MultipartBodyLengthLimit=maximumUploadBytes+1024*1024);
builder.Services.AddFvnAiInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks();
var app=builder.Build();
var replayCleanupCounter = 0;
app.MapHealthChecks("/health");
app.MapGet("/health/ready",async (IServiceProvider sp,IOptions<AIPlatformOptions> aiOptions,IOptions<MalwareScannerOptions> scannerOptions,IHttpClientFactory clients,CancellationToken ct)=>{
 var db=sp.GetService<AiPlatformDbContext>();
 var databaseConnected=false;
 try{databaseConnected=db is not null&&await db.Database.CanConnectAsync(ct);}
 catch(Exception ex) when(ex is not OperationCanceledException||!ct.IsCancellationRequested){app.Logger.LogWarning(ex,"AI readiness check could not connect to SQL Server.");}
 var ai=aiOptions.Value;
 var scannerConfigured=string.Equals(scannerOptions.Value.Provider,"ClamAV",StringComparison.OrdinalIgnoreCase);
 var providerConfigured=string.Equals(ai.DefaultProvider,"Ollama",StringComparison.OrdinalIgnoreCase)&&Uri.TryCreate(ai.Endpoint,UriKind.Absolute,out var modelEndpoint)&&(modelEndpoint.Scheme==Uri.UriSchemeHttp||modelEndpoint.Scheme==Uri.UriSchemeHttps)&&!string.IsNullOrWhiteSpace(ai.Model)&&!string.IsNullOrWhiteSpace(ai.EmbeddingModel);
 var chatModelAvailable=false;
 var embeddingModelAvailable=false;
 if(providerConfigured)
 {
  try
  {
   using var client=clients.CreateClient();
   client.Timeout=TimeSpan.FromSeconds(5);
   using var response=await client.GetAsync(new Uri(ai.Endpoint!.TrimEnd('/')+"/api/tags"),ct);
   if(response.IsSuccessStatusCode)
   {
    using var json=await System.Text.Json.JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct),cancellationToken:ct);
    if(json.RootElement.TryGetProperty("models",out var models)&&models.ValueKind==System.Text.Json.JsonValueKind.Array)
    {
     var names=models.EnumerateArray().Where(x=>x.TryGetProperty("name",out _)).Select(x=>x.GetProperty("name").GetString()??"").ToArray();
     static bool HasModel(string[] names,string? configured)=>!string.IsNullOrWhiteSpace(configured)&&names.Any(name=>string.Equals(name,configured,StringComparison.OrdinalIgnoreCase)||string.Equals(name,configured+":latest",StringComparison.OrdinalIgnoreCase));
     chatModelAvailable=HasModel(names,ai.Model);
     embeddingModelAvailable=HasModel(names,ai.EmbeddingModel);
    }
   }
  }
  catch(Exception ex) when((ex is HttpRequestException || ex is System.Text.Json.JsonException || ex is UriFormatException) || (ex is TaskCanceledException && !ct.IsCancellationRequested))
  {
   app.Logger.LogWarning(ex,"AI readiness check could not query Ollama model inventory.");
  }
 }
 var ready=databaseConnected&&providerConfigured&&chatModelAvailable&&embeddingModelAvailable&&scannerConfigured;
 var result=new{status=ready?"ready":"not-ready",database=databaseConnected?"connected":"unavailable",provider=ai.DefaultProvider,model=ai.Model,embeddingModel=ai.EmbeddingModel,malwareScannerProvider=scanner.Provider,providerConfigured,chatModelAvailable,embeddingModelAvailable,malwareScannerConfigured=scannerConfigured};
 return ready?Results.Ok(result):Results.Json(result,statusCode:503);
});
app.MapGet("/api/ai/capabilities",(IOptions<AIPlatformOptions> o)=>{
 var s=o.Value;
 var providerConfigured=string.Equals(s.DefaultProvider,"Ollama",StringComparison.OrdinalIgnoreCase)&&Uri.TryCreate(s.Endpoint,UriKind.Absolute,out var endpoint)&&(endpoint.Scheme==Uri.UriSchemeHttp||endpoint.Scheme==Uri.UriSchemeHttps)&&!string.IsNullOrWhiteSpace(s.Model)&&!string.IsNullOrWhiteSpace(s.EmbeddingModel);
 return Results.Ok(new{service="FVN_REGISTER.AI",deploymentMode=s.DeploymentMode,provider=s.DefaultProvider,providerConfigured,modelConfigured=!string.IsNullOrWhiteSpace(s.Model),embeddingModelConfigured=!string.IsNullOrWhiteSpace(s.EmbeddingModel),malwareScannerConfigured=string.Equals(app.Configuration["AIPlatform:MalwareScanner:Provider"],"ClamAV",StringComparison.OrdinalIgnoreCase),capabilities=new[]{"multipart-ingestion","quarantine-storage","sql-job-queue","document-status","pdf-text-extraction","docx-text-extraction","image-ocr","scanned-pdf-ocr","generic-document-classification","schema-registry","schema-driven-extraction","extraction-validation","human-review-persistence","feedback-capture","feedback-evaluation","knowledge-publication","acl-scoped-vector-search","hr-evidence-reconciliation","hr-case-inbox","hr-case-review-audit"},maximumFileSizeMb=s.MaximumFileSizeMb,businessWriteRequiresHumanApproval=s.RequireHumanApprovalBeforeBusinessWrite});
});
var group=app.MapGroup("/api/ai/v1").AddEndpointFilter(async (ctx,next)=>{
 var configured=app.Configuration[$"{AIPlatformOptions.SectionName}:ApiKey"];
 if(string.IsNullOrWhiteSpace(configured)) return Results.Problem("AI API key is not configured. Upload endpoints are disabled.",statusCode:503);
 if(!ctx.HttpContext.Request.Headers.TryGetValue("X-AI-API-Key",out var supplied)) return Results.Unauthorized();
 var a=Encoding.UTF8.GetBytes(configured); var b=Encoding.UTF8.GetBytes(supplied.ToString());
 if(a.Length!=b.Length||!CryptographicOperations.FixedTimeEquals(a,b)) return Results.Unauthorized();

 var request=ctx.HttpContext.Request;
 var tenant=request.Headers["X-Tenant-Code"].ToString().Trim();
 var actor=request.Headers["X-Actor-Id"].ToString().Trim();
 var timestamp=request.Headers["X-AI-Timestamp"].ToString();
 var nonce=request.Headers["X-AI-Nonce"].ToString();
 var signature=request.Headers["X-AI-Signature"].ToString();
 var signingKey=app.Configuration[$"{AIPlatformOptions.SectionName}:ContextSigningKey"];
 var bindContent = settings.RequireContentBoundSignatures;
 var bodySha256 = string.Empty;
 if (bindContent)
 {
  request.EnableBuffering();
  using var hash = SHA256.Create();
  var digest = await hash.ComputeHashAsync(request.Body, ctx.HttpContext.RequestAborted);
  bodySha256 = Convert.ToHexString(digest);
  request.Body.Position = 0;
 }
 if(!RequestIdentitySignature.TryValidate(signingKey??string.Empty,request.Method,request.Path.Value??"/",
     tenant,actor,timestamp,nonce,signature,DateTimeOffset.UtcNow,out var identityFailure,
     bindContent ? request.QueryString.Value ?? string.Empty : string.Empty, bodySha256))
 {
  if(string.IsNullOrWhiteSpace(signingKey)||signingKey.Length<32)
   return Results.Problem(identityFailure,statusCode:503);
  return Results.Unauthorized();
 }

 // Persist the nonce so replay attempts are rejected across API replicas.
 var replayDb = ctx.HttpContext.RequestServices.GetService<AiPlatformDbContext>();
 if (replayDb is null)
  return Results.Problem("AI SQL connection is required for replay protection.", statusCode:503);
 try
 {
  await replayDb.Database.ExecuteSqlInterpolatedAsync(
   $"INSERT INTO ai.AiRequestNonces(TenantCode, Nonce, ExpiresAtUtc) VALUES ({tenant}, {nonce}, {DateTimeOffset.UtcNow.AddMinutes(5)})",
   ctx.HttpContext.RequestAborted);
 }
 catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 2601 or 2627)
 {
  return Results.Unauthorized();
 }
 if ((Interlocked.Increment(ref replayCleanupCounter) & 0xFF) == 0)
 {
  await replayDb.Database.ExecuteSqlRawAsync(
   "DELETE TOP (1000) FROM ai.AiRequestNonces WHERE ExpiresAtUtc < TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')",
   ctx.HttpContext.RequestAborted);
 }
 return await next(ctx);
});
group.MapPost("/documents",async (HttpRequest req,IServiceProvider services,IOptions<AIPlatformOptions> o,CancellationToken ct)=>{
 var service=services.GetService<IDocumentIngestionService>();
 if(service is null) return Results.Problem("AI SQL connection is not configured. Apply the AI schema and configure ConnectionStrings:FvnAi.",statusCode:503);
 var tenant=req.Headers["X-Tenant-Code"].ToString().Trim(); var actor=req.Headers["X-Actor-Id"].ToString().Trim();
 if(string.IsNullOrWhiteSpace(tenant)||tenant.Length>64||string.IsNullOrWhiteSpace(actor)||actor.Length>128) return Results.BadRequest(new{error="X-Tenant-Code and X-Actor-Id headers are required."});
 if(!req.HasFormContentType) return Results.BadRequest(new{error="multipart/form-data is required."});
 var form=await req.ReadFormAsync(ct); var file=form.Files.GetFile("file");
 if(file is null||file.Length<=0) return Results.BadRequest(new{error="A non-empty file field is required."});
 var max=(long)Math.Clamp(o.Value.MaximumFileSizeMb,1,250)*1024*1024;
 if(file.Length>max) return Results.StatusCode(413);
 await using var stream=file.OpenReadStream();
 try
 {
  var moduleCode=form["moduleCode"].FirstOrDefault();
  var schemaCode=form["schemaCode"].FirstOrDefault();
  var receipt=await service.IngestAsync(tenant,actor,file.FileName,stream,ct,moduleCode,schemaCode);
  return Results.Accepted($"/api/ai/v1/documents/{receipt.DocumentId}",receipt);
 }
 catch(InvalidDataException ex){return Results.BadRequest(new{error=ex.Message});}
 catch(ArgumentException ex){return Results.BadRequest(new{error=ex.Message});}
}).DisableAntiforgery();
group.MapGet("/documents/{id:guid}",async (Guid id,HttpRequest req,IServiceProvider services,CancellationToken ct)=>{
 var service=services.GetService<IDocumentIngestionService>();
 if(service is null) return Results.Problem("AI SQL connection is not configured. Apply the AI schema and configure ConnectionStrings:FvnAi.",statusCode:503);
 var tenant=req.Headers["X-Tenant-Code"].ToString().Trim();
 var actor=req.Headers["X-Actor-Id"].ToString().Trim();
 if(string.IsNullOrWhiteSpace(tenant)||tenant.Length>64||string.IsNullOrWhiteSpace(actor)) return Results.BadRequest(new{error="Signed tenant and actor context is required."});
 var db=services.GetService<AiPlatformDbContext>();
 if(db is null) return Results.Problem("AI SQL connection is not configured.",statusCode:503);
 var owned=await db.Documents.AsNoTracking().AnyAsync(x=>x.Id==id&&x.TenantCode==tenant&&x.UploadedBy==actor,ct);
 if(!owned) return Results.NotFound();
 var result=await service.GetStatusAsync(id,tenant,ct);
 return result is null?Results.NotFound():Results.Ok(result);
});
group.MapGet("/documents/{id:guid}/extraction",async (Guid id,HttpRequest req,IServiceProvider services,CancellationToken ct)=>{
 var service=services.GetService<IDocumentExtractionQueryService>();
 if(service is null) return Results.Problem("AI SQL connection is not configured.",statusCode:503);
 var tenant=req.Headers["X-Tenant-Code"].ToString().Trim();
 var actor=req.Headers["X-Actor-Id"].ToString().Trim();
 var result=await service.GetLatestAsync(id,tenant,actor,ct);
 return result is null?Results.NotFound():Results.Ok(result);
});
group.MapPost("/reviews/{extractionId:guid}",async (Guid extractionId,ReviewRequest body,HttpRequest req,IServiceProvider services,CancellationToken ct)=>{
 var service=services.GetService<IDocumentReviewService>();
 var authorization=services.GetService<IAuthorizedDocumentReviewProvider>();
 if(service is null) return Results.Problem("AI SQL connection is not configured.",statusCode:503);
 if(authorization is null) return Results.Problem("Trusted document-review authorization is not configured.",statusCode:503);
 var tenant=req.Headers["X-Tenant-Code"].ToString().Trim();
 var actor=req.Headers["X-Actor-Id"].ToString().Trim();
 try
 {
  await authorization.AuthorizeReviewAsync(tenant,actor,extractionId,ct);
  var receipt=await service.ReviewAsync(extractionId,tenant,actor,body.Decision,body.CorrectedPayloadJson,body.Comment,ct);
  return Results.Ok(receipt);
 }
 catch(KeyNotFoundException){return Results.NotFound();}
 catch(UnauthorizedAccessException){return Results.Forbid();}
 catch(InvalidOperationException ex) when(ex.Message=="DOCUMENT_REVIEW_AUTHORIZATION_NOT_CONFIGURED")
 { return Results.Problem("Trusted FVN_REGISTER reviewer authorization adapter is not configured.",statusCode:503); }
 catch(ArgumentException ex){return Results.BadRequest(new{error=ex.Message});}
 catch(System.Text.Json.JsonException){return Results.BadRequest(new{error="Corrected payload must contain valid JSON."});}
 });
group.MapPost("/feedback",async (FeedbackRequest body,HttpRequest req,IServiceProvider services,CancellationToken ct)=>{
 var service=services.GetService<IFeedbackService>();
 if(service is null) return Results.Problem("AI SQL connection is not configured.",statusCode:503);
 var tenant=req.Headers["X-Tenant-Code"].ToString().Trim();
 var actor=req.Headers["X-Actor-Id"].ToString().Trim();
 try
 {
  await service.RecordAsync(tenant,actor,body.DocumentId,body.ExtractionId,body.FeedbackType,
   body.BeforeJson,body.AfterJson,body.Reason,ct);
  return Results.Accepted();
 }
 catch(KeyNotFoundException){return Results.NotFound();}
 catch(ArgumentException ex){return Results.BadRequest(new{error=ex.Message});}
 catch(System.Text.Json.JsonException){return Results.BadRequest(new{error="Feedback payload must contain valid JSON."});}
});

group.MapGet("/schemas/{moduleCode}/{schemaCode}", async (string moduleCode, string schemaCode,
    HttpRequest req, IServiceProvider services, CancellationToken ct) =>
{
    var registry = services.GetService<IExtractionSchemaRegistry>();
    if (registry is null) return Results.Problem("Extraction schema registry is not configured.", statusCode: 503);
    var tenant = req.Headers["X-Tenant-Code"].ToString().Trim();
    var schema = await registry.GetPublishedAsync(tenant, moduleCode, schemaCode, ct);
    return schema is null ? Results.NotFound() : Results.Ok(schema);
});

group.MapPost("/schemas/drafts", async (SchemaDraftRequest body, HttpRequest req,
    IServiceProvider services, CancellationToken ct) =>
{
    var registry = services.GetService<IExtractionSchemaRegistry>();
    var authorization = services.GetService<IAuthorizedSchemaManagementProvider>();
    if (registry is null) return Results.Problem("Extraction schema registry is not configured.", statusCode: 503);
    if (authorization is null) return Results.Problem("Trusted schema authorization is not configured.", statusCode: 503);
    var tenant = req.Headers["X-Tenant-Code"].ToString().Trim();
    var actor = req.Headers["X-Actor-Id"].ToString().Trim();
    try
    {
        await authorization.AuthorizeDraftAsync(tenant, actor, body.ModuleCode, body.Schema.SchemaCode, ct);
        var version = await registry.CreateDraftVersionAsync(tenant, body.ModuleCode, body.Schema, actor, ct);
        return Results.Created($"/api/ai/v1/schemas/{body.ModuleCode}/{body.Schema.SchemaCode}", new { version });
    }
    catch (UnauthorizedAccessException) { return Results.Forbid(); }
    catch (InvalidOperationException ex) when (ex.Message == "SCHEMA_ADMIN_AUTHORIZATION_NOT_CONFIGURED")
    { return Results.Problem("Trusted FVN_REGISTER schema-administration adapter is not configured.", statusCode: 503); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

group.MapPost("/schemas/{moduleCode}/{schemaCode}/{version:int}/publish", async (string moduleCode,
    string schemaCode, int version, HttpRequest req, IServiceProvider services, CancellationToken ct) =>
{
    var registry = services.GetService<IExtractionSchemaRegistry>();
    var authorization = services.GetService<IAuthorizedSchemaManagementProvider>();
    if (registry is null) return Results.Problem("Extraction schema registry is not configured.", statusCode: 503);
    if (authorization is null) return Results.Problem("Trusted schema authorization is not configured.", statusCode: 503);
    var tenant = req.Headers["X-Tenant-Code"].ToString().Trim();
    var actor = req.Headers["X-Actor-Id"].ToString().Trim();
    try
    {
        await authorization.AuthorizePublishAsync(tenant, actor, moduleCode, schemaCode, version, ct);
        await registry.PublishVersionAsync(tenant, moduleCode, schemaCode, version, actor, ct);
        return Results.Ok(new { moduleCode, schemaCode, version, status = "Published" });
    }
    catch (KeyNotFoundException) { return Results.NotFound(); }
    catch (UnauthorizedAccessException) { return Results.Forbid(); }
    catch (InvalidOperationException ex) when (ex.Message == "SCHEMA_ADMIN_AUTHORIZATION_NOT_CONFIGURED")
    { return Results.Problem("Trusted FVN_REGISTER schema-administration adapter is not configured.", statusCode: 503); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
});

group.MapPost("/knowledge/{documentId:guid}/publish", async (Guid documentId, KnowledgePublishRequest body,
    HttpRequest req, IServiceProvider services, CancellationToken ct) =>
{
    var indexing = services.GetService<IKnowledgeIndexingService>();
    var access = services.GetService<IAuthorizedKnowledgeAccessProvider>();
    if (indexing is null) return Results.Problem("Knowledge indexing is not configured.", statusCode: 503);
    if (access is null) return Results.Problem("Trusted knowledge authorization is not configured.", statusCode: 503);
    var tenant = req.Headers["X-Tenant-Code"].ToString().Trim();
    var actor = req.Headers["X-Actor-Id"].ToString().Trim();
    try
    {
        var scopes = await access.GetPublicationScopesAsync(tenant, actor, documentId, body.ReviewId, ct);
        if (scopes.Count == 0) return Results.Forbid();
        var count = await indexing.PublishApprovedDocumentAsync(tenant, documentId, body.ReviewId, scopes, ct);
        return Results.Ok(new { documentId, indexedChunks = count, status = "Published" });
    }
    catch (KeyNotFoundException) { return Results.NotFound(); }
    catch (UnauthorizedAccessException) { return Results.Forbid(); }
    catch (InvalidOperationException ex) when (ex.Message == "KNOWLEDGE_AUTHORIZATION_PROVIDER_NOT_CONFIGURED")
    { return Results.Problem("Trusted FVN_REGISTER knowledge authorization adapter is not configured.", statusCode: 503); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

group.MapPost("/knowledge/{documentId:guid}/unpublish", async (Guid documentId,
    HttpRequest req, IServiceProvider services, CancellationToken ct) =>
{
    var indexing = services.GetService<IKnowledgeIndexingService>();
    var access = services.GetService<IAuthorizedKnowledgeAccessProvider>();
    if (indexing is null) return Results.Problem("Knowledge indexing is not configured.", statusCode: 503);
    if (access is null) return Results.Problem("Trusted knowledge authorization is not configured.", statusCode: 503);
    var tenant = req.Headers["X-Tenant-Code"].ToString().Trim();
    var actor = req.Headers["X-Actor-Id"].ToString().Trim();
    try
    {
        await access.AuthorizeUnpublishAsync(tenant, actor, documentId, ct);
        await indexing.UnpublishDocumentAsync(tenant, documentId, ct);
        return Results.Ok(new { documentId, status = "Unpublished" });
    }
    catch (UnauthorizedAccessException) { return Results.Forbid(); }
    catch (InvalidOperationException ex) when (ex.Message == "KNOWLEDGE_AUTHORIZATION_PROVIDER_NOT_CONFIGURED")
    { return Results.Problem("Trusted FVN_REGISTER knowledge authorization adapter is not configured.", statusCode: 503); }
});

group.MapPost("/knowledge/search", async (KnowledgeSearchRequest body, HttpRequest req,
    IServiceProvider services, CancellationToken ct) =>
{
    var search = services.GetService<IKnowledgeSearchService>();
    var access = services.GetService<IAuthorizedKnowledgeAccessProvider>();
    if (search is null) return Results.Problem("Knowledge search is not configured.", statusCode: 503);
    if (access is null) return Results.Problem("Trusted knowledge authorization is not configured.", statusCode: 503);
    var tenant = req.Headers["X-Tenant-Code"].ToString().Trim();
    var actor = req.Headers["X-Actor-Id"].ToString().Trim();
    try
    {
        var scopes = await access.GetSearchScopesAsync(tenant, actor, ct);
        return Results.Ok(await search.SearchAsync(tenant, actor, scopes, body.Query, body.Limit, ct));
    }
    catch (UnauthorizedAccessException) { return Results.Forbid(); }
    catch (InvalidOperationException ex) when (ex.Message == "KNOWLEDGE_AUTHORIZATION_PROVIDER_NOT_CONFIGURED")
    { return Results.Problem("Trusted FVN_REGISTER knowledge authorization adapter is not configured.", statusCode: 503); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

group.MapPost("/rag/answer", async (RagAnswerRequest body, HttpRequest req,
    IServiceProvider services, CancellationToken ct) =>
{
    var rag = services.GetService<IRagAnswerService>();
    var access = services.GetService<IAuthorizedKnowledgeAccessProvider>();
    if (rag is null) return Results.Problem("RAG answer service is not configured.", statusCode: 503);
    if (access is null) return Results.Problem("Trusted knowledge authorization is not configured.", statusCode: 503);
    var tenant = req.Headers["X-Tenant-Code"].ToString().Trim();
    var actor = req.Headers["X-Actor-Id"].ToString().Trim();
    try
    {
        var scopes = await access.GetSearchScopesAsync(tenant, actor, ct);
        return Results.Ok(await rag.AnswerAsync(tenant, actor, scopes, body.Query,
            body.RetrievalLimit, ct));
    }
    catch (UnauthorizedAccessException) { return Results.Forbid(); }
    catch (InvalidOperationException ex) when (ex.Message == "KNOWLEDGE_AUTHORIZATION_PROVIDER_NOT_CONFIGURED")
    { return Results.Problem("Trusted FVN_REGISTER knowledge authorization adapter is not configured.", statusCode: 503); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

group.MapPost("/feedback/evaluation", async (FeedbackEvaluationRequest body, HttpRequest req,
    IServiceProvider services, CancellationToken ct) =>
{
    var evaluation = services.GetService<IFeedbackEvaluationService>();
    if (evaluation is null) return Results.Problem("Feedback evaluation is not configured.", statusCode: 503);
    var tenant = req.Headers["X-Tenant-Code"].ToString().Trim();
    try { return Results.Ok(await evaluation.GetSummaryAsync(tenant, body.FromUtc, body.ToUtc, ct)); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

group.MapPost("/hr-cases/analyze", async (HRCaseAnalysisRequest body, HttpRequest req,
    IServiceProvider services, CancellationToken ct) =>
{
    var service = services.GetService<IHRReconciliationService>();
    if (service is null) return Results.Problem("HR reconciliation service is not configured.", statusCode: 503);
    var tenant = req.Headers["X-Tenant-Code"].ToString().Trim();
    var actor = req.Headers["X-Actor-Id"].ToString().Trim();
    try
    {
        var result = await service.AnalyzeAsync(tenant, actor, body, ct);
        return Results.Ok(result);
    }
    catch (KeyNotFoundException) { return Results.NotFound(); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    catch (InvalidOperationException ex) when (ex.Message.StartsWith("HR_FACTS_PROVIDER_NOT_CONFIGURED", StringComparison.Ordinal))
    { return Results.Problem("Trusted FVN_REGISTER HR facts adapter is not configured.", statusCode: 503); }
});



group.MapPost("/hr-cases/analyze-authorized", async (AuthorizedHRCaseAnalysisRequest body, HttpRequest req,
    IServiceProvider services, CancellationToken ct) =>
{
    var service = services.GetService<IHRReconciliationService>();
    if (service is null) return Results.Problem("HR reconciliation service is not configured.", statusCode: 503);
    var tenant = req.Headers["X-Tenant-Code"].ToString().Trim();
    var actor = req.Headers["X-Actor-Id"].ToString().Trim();
    try
    {
        var result = await service.AnalyzeWithAuthorizedFactsAsync(tenant, actor, body, ct);
        return Results.Ok(result);
    }
    catch (KeyNotFoundException) { return Results.NotFound(); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

group.MapGet("/hr-cases/pending", async (int? limit, HttpRequest req, IServiceProvider services, CancellationToken ct) =>
{
    var service = services.GetService<IHRCaseQueryService>();
    if (service is null) return Results.Problem("HR case query service is not configured.", statusCode: 503);
    var tenant = req.Headers["X-Tenant-Code"].ToString().Trim();
    var actor = req.Headers["X-Actor-Id"].ToString().Trim();
    try { return Results.Ok(await service.GetPendingAsync(tenant, actor, limit ?? 50, ct)); }
    catch (UnauthorizedAccessException) { return Results.Forbid(); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

group.MapGet("/hr-cases/{caseId:guid}", async (Guid caseId, HttpRequest req, IServiceProvider services, CancellationToken ct) =>
{
    var service = services.GetService<IHRCaseQueryService>();
    if (service is null) return Results.Problem("HR case query service is not configured.", statusCode: 503);
    var tenant = req.Headers["X-Tenant-Code"].ToString().Trim();
    var actor = req.Headers["X-Actor-Id"].ToString().Trim();
    try
    {
        var item = await service.GetAsync(tenant, actor, caseId, ct);
        return item is null ? Results.NotFound() : Results.Ok(item);
    }
    catch (UnauthorizedAccessException) { return Results.Forbid(); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

group.MapPost("/hr-cases/{caseId:guid}/review", async (Guid caseId, HRCaseReviewRequest body,
    HttpRequest req, IServiceProvider services, CancellationToken ct) =>
{
    var service = services.GetService<IHRCaseReviewService>();
    if (service is null) return Results.Problem("HR case review service is not configured.", statusCode: 503);
    var tenant = req.Headers["X-Tenant-Code"].ToString().Trim();
    var actor = req.Headers["X-Actor-Id"].ToString().Trim();
    try
    {
        var result = await service.ReviewAsync(tenant, actor, caseId, body, ct);
        return Results.Ok(result);
    }
    catch (UnauthorizedAccessException) { return Results.Forbid(); }
    catch (KeyNotFoundException) { return Results.NotFound(); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
});

app.Run();
public sealed record FeedbackRequest(Guid? DocumentId,Guid? ExtractionId,string FeedbackType,string? BeforeJson,string? AfterJson,string? Reason);
public sealed record ReviewRequest(string Decision,string? CorrectedPayloadJson,string? Comment);
public partial class Program { }
public sealed record SchemaDraftRequest(string ModuleCode, FVN_REGISTER.AI.Application.Contracts.ExtractionSchema Schema);
public sealed record KnowledgePublishRequest(Guid ReviewId);
public sealed record KnowledgeSearchRequest(string Query, int Limit);
public sealed record FeedbackEvaluationRequest(DateTimeOffset FromUtc, DateTimeOffset ToUtc);
public sealed record RagAnswerRequest(string Query, int RetrievalLimit);
