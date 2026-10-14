using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Application.Configuration;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using FVN_REGISTER.AI.Infrastructure.Providers;
using FVN_REGISTER.AI.Infrastructure.Storage;
using FVN_REGISTER.AI.Infrastructure.Services;
using FVN_REGISTER.AI.Infrastructure.Validation;
using FVN_REGISTER.AI.Infrastructure.Extraction;
using FVN_REGISTER.AI.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace FVN_REGISTER.AI.Infrastructure;
public static class DependencyInjection
{
 public static IServiceCollection AddFvnAiInfrastructure(this IServiceCollection services,IConfiguration configuration)
 {
  services.Configure<AIPlatformOptions>(configuration.GetSection(AIPlatformOptions.SectionName));
  services.Configure<MalwareScannerOptions>(configuration.GetSection(MalwareScannerOptions.SectionName));
  var options=configuration.GetSection(AIPlatformOptions.SectionName).Get<AIPlatformOptions>()??new AIPlatformOptions();
  var connection=configuration.GetConnectionString(options.ConnectionStringName);
  if(!string.IsNullOrWhiteSpace(connection)) services.AddDbContext<AiPlatformDbContext>(db=>db.UseSqlServer(connection,sql=>sql.EnableRetryOnFailure(3)));
  services.TryAddSingleton<IAIModelProvider,UnconfiguredAIModelProvider>();
  services.AddSingleton<IOriginalDocumentStore,FileSystemOriginalDocumentStore>();
  services.AddSingleton<IUploadContentValidator,UploadContentValidator>();
  services.AddSingleton<IDocumentTextExtractor,LocalDocumentTextExtractor>();
  services.TryAddSingleton<IMalwareScanner>(sp =>
  {
   var scannerOptions=sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MalwareScannerOptions>>().Value;
   return scannerOptions.Provider.Equals("ClamAV",StringComparison.OrdinalIgnoreCase)
    ? new ClamDMalwareScanner(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MalwareScannerOptions>>())
    : new UnconfiguredMalwareScanner();
  });
  services.AddScoped<IDocumentIngestionService,DocumentIngestionService>();
  services.AddScoped<IDocumentReviewService,DocumentReviewService>();
  services.AddScoped<IDocumentExtractionQueryService,DocumentExtractionQueryService>();
  services.AddScoped<IFeedbackService,FeedbackService>();
  services.AddScoped<IIngestionQueue,SqlIngestionQueue>();
  services.AddScoped<ILeasedIngestionQueue,SqlIngestionQueue>();
  services.AddScoped<IDocumentPipeline,DocumentPipeline>();
  services.TryAddScoped<IAuthorizedHRFactsProvider,UnconfiguredAuthorizedHRFactsProvider>();
  services.AddScoped<IHRReconciliationService,HRReconciliationService>();
  if(configuration.GetValue<bool>("AIPlatform:TrustFvnRegisterGatewayForHRReview"))
   services.AddScoped<IHRCaseReviewerAuthorization,GatewayHRCaseReviewerAuthorization>();
  else
   services.TryAddScoped<IHRCaseReviewerAuthorization,UnconfiguredHRCaseReviewerAuthorization>();
  services.AddScoped<IHRCaseReviewService,HRCaseReviewService>();
  services.AddScoped<IHRCaseQueryService,HRCaseQueryService>();
  return services;
 }
}