using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Configuration;
using FVN_REGISTER.AI.Infrastructure.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace FVN_REGISTER.AI.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddFvnAiInfrastructure(this IServiceCollection services,IConfiguration configuration)
    {
        services.Configure<AIPlatformOptions>(configuration.GetSection(AIPlatformOptions.SectionName));
        services.TryAddSingleton<IAIModelProvider,UnconfiguredAIModelProvider>();
        return services;
    }
}