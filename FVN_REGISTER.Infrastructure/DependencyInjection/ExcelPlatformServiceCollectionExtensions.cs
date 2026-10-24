using FVN_REGISTER.Application.Interfaces.Excel;
using FVN_REGISTER.Application.Interfaces.OT;
using FVN_REGISTER.Infrastructure.Services.Excel;
using FVN_REGISTER.Infrastructure.Services.OT;
using Microsoft.Extensions.DependencyInjection;

namespace FVN_REGISTER.Infrastructure.DependencyInjection;

public static class ExcelPlatformServiceCollectionExtensions
{
    public static IServiceCollection AddSharedExcelPlatform(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IExcelPlatform, ExcelPlatform>();
        // Shared infrastructure services are registered here because this extension is loaded by API startup.
        services.AddScoped<IOtRegistrationPolicy, OtRegistrationPolicy>();
        services.AddScoped<OTQueryService>();
        services.AddScoped<IOTQueryService>(sp => sp.GetRequiredService<PolicyAwareOTQueryService>());
        services.AddScoped<PolicyAwareOTQueryService>();
        return services;
    }
}
