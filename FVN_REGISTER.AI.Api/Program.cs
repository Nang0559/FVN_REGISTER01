using System.Security.Cryptography;
using System.Text;
using FVN_REGISTER.AI.Api.Security;
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Configuration;
using FVN_REGISTER.AI.Infrastructure;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
var builder=WebApplication.CreateBuilder(args);
var settings=builder.Configuration.GetSection(AIPlatformOptions.SectionName).Get<AIPlatformOptions>()??new AIPlatformOptions();
builder.WebHost.ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=(long)Math.Clamp(settings.MaximumFileSizeMb,1,250)*1024*1024+1024*1024);
builder.Services.AddFvnAiInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks();
var app=builder.Build();
app.MapHealthChecks("/health");
app.MapGet("/health/ready",async (IServiceProvider sp,CancellationToken ct)=>{
 var db=sp.GetService<AiPlatformDbContext>();
 if(db is null) return Results.Problem("AI SQL connection is not configured.",statusCode:503);
 return await db.Database.CanConnectAsync(ct)?Results.Ok(new{status="ready",database="connected"}):Results.Problem("AI SQL database is unavailable.",statusCode:503);
});
app.MapGet("/api/ai/capabilities",(IOptions<AIPlatformOptions> o)=>{
 var s=o.Value;
 return Results.Ok(new{service="FVN_REGISTER.AI",deploymentMode=s.DeploymentMode,provider=s.DefaultProvider,providerConfigured=!string.Equals(s.DefaultProvider,"Unconfigured",StringComparison.OrdinalIgnoreCase),capabilities=new[]{"multipart-ingestion","quarantine-storage","sql-job-queue","document-status","human-review-persistence","feedback-capture"},maximumFileSizeMb=s.MaximumFileSizeMb,businessWriteRequiresHumanApproval=s.RequireHumanApprovalBeforeBusinessWrite});
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
 if(!RequestIdentitySignature.TryValidate(signingKey??string.Empty,request.Method,request.Path.Value??"/",
     tenant,actor,timestamp,nonce,signature,DateTimeOffset.UtcNow,out var identityFailure))
 {
  if(string.IsNullOrWhiteSpace(signingKey)||signingKey.Length<32)
   return Results.Problem(identityFailure,statusCode:503);
  return Results.Unauthorized();
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
  var receipt=await service.IngestAsync(tenant,actor,file.FileName,stream,ct);
  return Results.Accepted($"/api/ai/v1/documents/{receipt.DocumentId}",receipt);
 }
 catch(InvalidDataException ex){return Results.BadRequest(new{error=ex.Message});}
 catch(ArgumentException ex){return Results.BadRequest(new{error=ex.Message});}
}).DisableAntiforgery();
group.MapGet("/documents/{id:guid}",async (Guid id,HttpRequest req,IServiceProvider services,CancellationToken ct)=>{
 var service=services.GetService<IDocumentIngestionService>();
 if(service is null) return Results.Problem("AI SQL connection is not configured. Apply the AI schema and configure ConnectionStrings:FvnAi.",statusCode:503);
 var tenant=req.Headers["X-Tenant-Code"].ToString().Trim();
 if(string.IsNullOrWhiteSpace(tenant)||tenant.Length>64) return Results.BadRequest(new{error="X-Tenant-Code is required."});
 var result=await service.GetStatusAsync(id,tenant,ct);
 return result is null?Results.NotFound():Results.Ok(result);
});
group.MapPost("/reviews/{extractionId:guid}",async (Guid extractionId,ReviewRequest body,HttpRequest req,IServiceProvider services,CancellationToken ct)=>{
 var service=services.GetService<IDocumentReviewService>();
 if(service is null) return Results.Problem("AI SQL connection is not configured.",statusCode:503);
 var tenant=req.Headers["X-Tenant-Code"].ToString().Trim();
 var actor=req.Headers["X-Actor-Id"].ToString().Trim();
 try
 {
  var receipt=await service.ReviewAsync(extractionId,tenant,actor,body.Decision,body.CorrectedPayloadJson,body.Comment,ct);
  return Results.Ok(receipt);
 }
 catch(KeyNotFoundException){return Results.NotFound();}
 catch(ArgumentException ex){return Results.BadRequest(new{error=ex.Message});}
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
app.Run();
public sealed record FeedbackRequest(Guid? DocumentId,Guid? ExtractionId,string FeedbackType,string? BeforeJson,string? AfterJson,string? Reason);
public sealed record ReviewRequest(string Decision,string? CorrectedPayloadJson,string? Comment);
public partial class Program { }