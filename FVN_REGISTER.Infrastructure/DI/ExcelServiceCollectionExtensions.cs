using FVN_REGISTER.Application.Interfaces.Excel;
using FVN_REGISTER.Infrastructure.Services.Excel;
using Microsoft.Extensions.DependencyInjection;

namespace FVN_REGISTER.Infrastructure.DI;

public static class ExcelServiceCollectionExtensions
{
    public static IServiceCollection AddSharedExcelPlatform(this IServiceCollection services)
    {
        services.AddScoped<IExcelPlatform, ExcelPlatform>();
        return services;
    }
}
