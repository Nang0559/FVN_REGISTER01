using FVN_REGISTER.Application.Interfaces.Excel;
using FVN_REGISTER.Infrastructure.Services.Excel;
using Microsoft.Extensions.DependencyInjection;

namespace FVN_REGISTER.Infrastructure.DependencyInjection;

public static class ExcelPlatformServiceCollectionExtensions
{
    public static IServiceCollection AddSharedExcelPlatform(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IExcelPlatform, ExcelPlatform>();
        return services;
    }
}
