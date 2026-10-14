using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Configuration;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using FVN_REGISTER.AI.Infrastructure.Providers;
using FVN_REGISTER.AI.Infrastructure.Storage;
using FVN_REGISTER.AI.Infrastructure.Services;
using FVN_REGISTER.AI.Infrastructure.Validation;
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
  var options=configuration.GetSection(AIPlatformOptions.SectionName).Get<AIPlatformOptions>()??new AIPlatformOptions();
  var connection=configuration.GetConnectionString(options.ConnectionStringName);
  if(!string.IsNullOrWhiteSpace(connection)) services.AddDbContext<AiPlatformDbContext>(db=>db.UseSqlServer(connection,sql=>sql.EnableRetryOnFailure(3)));
  services.TryAddSingleton<IAIModelProvider,UnconfiguredAIModelProvider>();
  services.AddSingleton<IOriginalDocumentStore,FileSystemOriginalDocumentStore>();
  services.AddSingleton<IUploadContentValidator,UploadContentValidator>();
  services.AddScoped<IDocumentIngestionService,DocumentIngestionService>();
  services.AddScoped<IIngestionQueue,SqlIngestionQueue>();
  return services;
 }
}