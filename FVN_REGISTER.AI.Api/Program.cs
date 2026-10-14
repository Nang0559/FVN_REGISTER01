using FVN_REGISTER.AI.Application.Configuration;
using FVN_REGISTER.AI.Infrastructure;
var builder=WebApplication.CreateBuilder(args);
builder.Services.AddFvnAiInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks();
var app=builder.Build();
app.MapHealthChecks("/health");
app.MapGet("/api/ai/capabilities",(Microsoft.Extensions.Options.IOptions<AIPlatformOptions> options)=>{
    var s=options.Value;
    return Results.Ok(new { service="FVN_REGISTER.AI",deploymentMode=s.DeploymentMode,provider=s.DefaultProvider,
        providerConfigured=!string.Equals(s.DefaultProvider,"Unconfigured",StringComparison.OrdinalIgnoreCase),
        capabilities=new[]{"ingestion","document-understanding","structured-extraction","knowledge-rag","human-feedback"},
        businessWriteRequiresHumanApproval=s.RequireHumanApprovalBeforeBusinessWrite });
});
app.Run();