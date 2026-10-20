using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Application.Configuration;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using FVN_REGISTER.AI.Infrastructure.Providers;
using FVN_REGISTER.AI.Infrastructure.Storage;
using FVN_REGISTER.AI.Infrastructure.Services;
using FVN_REGISTER.AI.Infrastructure.Validation;
using FVN_REGISTER.AI.Infrastructure.Extraction;
using FVN_REGISTER.AI.Infrastructure.Connectors;
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
  services.Configure<AIPlatformOptions>(configuration.GetSection(AIPlatformOptions.SectionName)); services.Configure<KnowledgeSearchOptions>(configuration.GetSection("AIPlatform:KnowledgeSearch")); services.Configure<MalwareScannerOptions>(configuration.GetSection(MalwareScannerOptions.SectionName)); services.Configure<OcrOptions>(configuration.GetSection(OcrOptions.SectionName));
  var options=configuration.GetSection(AIPlatformOptions.SectionName).Get<AIPlatformOptions>()??new AIPlatformOptions();
  if(!options.RequireContentBoundSignatures) throw new InvalidOperationException("AIPlatform:RequireContentBoundSignatures must be true. Production startup is refused when signed requests do not bind query/body content.");
  var connection=configuration.GetConnectionString(options.ConnectionStringName);
  if(!string.IsNullOrWhiteSpace(connection)) services.AddDbContext<AiPlatformDbContext>(db=>db.UseSqlServer(connection,sql=>sql.EnableRetryOnFailure(3)));
  if(options.DefaultProvider.Equals("Ollama",StringComparison.OrdinalIgnoreCase))
  {
   if(!Uri.TryCreate(options.Endpoint,UriKind.Absolute,out var modelEndpoint)||(modelEndpoint.Scheme!=Uri.UriSchemeHttp&&modelEndpoint.Scheme!=Uri.UriSchemeHttps)) throw new InvalidOperationException("AIPlatform:Endpoint must be an absolute HTTP(S) URL when DefaultProvider is Ollama.");
   services.AddHttpClient<IAIModelProvider,OllamaAIModelProvider>(client=>{client.BaseAddress=new Uri(options.Endpoint!.TrimEnd('/')+"/");client.Timeout=TimeSpan.FromSeconds(120);}); services.AddHttpClient<IEmbeddingGenerator,OllamaEmbeddingGenerator>(client=>{client.BaseAddress=new Uri(options.Endpoint!.TrimEnd('/')+"/");client.Timeout=TimeSpan.FromSeconds(120);});
  } else { services.TryAddSingleton<IAIModelProvider,UnconfiguredAIModelProvider>(); services.TryAddSingleton<IEmbeddingGenerator,UnconfiguredEmbeddingGenerator>(); }
  services.AddSingleton<IOriginalDocumentStore,FileSystemOriginalDocumentStore>(); services.AddSingleton<IUploadContentValidator,UploadContentValidator>(); services.AddSingleton<IOcrTextExtractor,TesseractOcrTextExtractor>(); services.AddSingleton<IDocumentTextExtractor,LocalDocumentTextExtractor>();
  services.AddScoped<IDocumentClassifier,ModelDocumentClassifier>(); services.AddScoped<ISchemaDrivenDocumentExtractor,SchemaDrivenDocumentExtractor>(); services.AddScoped<IDocumentRepresentationExtractor,DocumentRepresentationExtractor>(); services.AddScoped<IExtractionFieldMappingService,ExtractionFieldMappingService>(); services.AddScoped<IDocumentSourceConnector,FolderDocumentSourceConnector>();
  services.TryAddSingleton<IMalwareScanner>(sp=>{var scannerOptions=sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MalwareScannerOptions>>().Value;return scannerOptions.Provider.Equals("ClamAV",StringComparison.OrdinalIgnoreCase)?new ClamDMalwareScanner(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MalwareScannerOptions>>()):new UnconfiguredMalwareScanner();});
  services.AddScoped<IDocumentIngestionService,DocumentIngestionService>(); services.AddScoped<IDocumentRetentionService,DocumentRetentionService>(); services.AddScoped<IDocumentReviewService,DocumentReviewService>(); services.AddScoped<IDocumentExtractionQueryService,DocumentExtractionQueryService>(); services.AddScoped<IExtractionValidationService,ExtractionValidationService>(); services.AddScoped<IExtractionSchemaRegistry,ExtractionSchemaRegistry>();
  services.AddScoped<IVectorKnowledgeStore,SqlVectorKnowledgeStore>(); services.AddScoped<IKnowledgeSearchService,KnowledgeSearchService>(); services.AddScoped<IRagAnswerService,RagAnswerService>(); services.AddScoped<IKnowledgeIndexingService,KnowledgeIndexingService>(); services.AddScoped<IKnowledgeLifecycleQueryService,KnowledgeLifecycleQueryService>(); services.TryAddScoped<IAuthorizedKnowledgeAccessProvider,UnconfiguredAuthorizedKnowledgeAccessProvider>(); services.TryAddScoped<IAuthorizedDocumentReviewProvider,UnconfiguredDocumentReviewProvider>(); services.TryAddScoped<IAuthorizedSchemaManagementProvider,UnconfiguredSchemaManagementProvider>(); services.AddScoped<IFeedbackService,FeedbackService>(); services.AddScoped<IFeedbackEvaluationService,FeedbackEvaluationService>();
  services.AddScoped<IGoldenDatasetService,GoldenDatasetService>(); services.AddScoped<IGoldenDatasetAdministration,GoldenDatasetService>(); services.AddScoped<IAIReleaseService,AIReleaseService>(); services.TryAddScoped<IAuthorizedAIPlatformAdministration,UnconfiguredAIPlatformAdministration>(); services.AddScoped<IHRCaseAssistantAdapter,HRCaseAssistantAdapter>();
  services.AddScoped<IIngestionQueue,FVN_REGISTER.AI.Infrastructure.Providers.SqlIngestionQueue>(); services.AddScoped<ILeasedIngestionQueue,FVN_REGISTER.AI.Infrastructure.Providers.SqlIngestionQueue>(); services.AddScoped<IDocumentPipeline,DocumentPipeline>();
  services.TryAddScoped<IAuthorizedHRFactsProvider,UnconfiguredAuthorizedHRFactsProvider>(); services.AddScoped<IHRReconciliationService,HRReconciliationService>(); if(configuration.GetValue<bool>("AIPlatform:TrustFvnRegisterGatewayForHRReview")) services.AddScoped<IHRCaseReviewerAuthorization,GatewayHRCaseReviewerAuthorization>(); else services.TryAddScoped<IHRCaseReviewerAuthorization,UnconfiguredHRCaseReviewerAuthorization>(); services.AddScoped<IHRCaseReviewService,HRCaseReviewService>(); services.AddScoped<IHRCaseQueryService,HRCaseQueryService>();
  return services;
 }
}