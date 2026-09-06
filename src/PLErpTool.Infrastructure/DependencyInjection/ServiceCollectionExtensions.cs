using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PLErpTool.Domain.Account;
using PLErpTool.Domain.Receivable;
using PLErpTool.Application.Account;
using PLErpTool.Application.Receivable;
using PLErpTool.Infrastructure.Excel;
using PLErpTool.Infrastructure.Import;
using PLErpTool.Infrastructure.Persistence;
using PLErpTool.Infrastructure.Persistence.Repositories;

namespace PLErpTool.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string dbPath)
    {
        services.AddDbContext<PlErpDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        services.AddScoped<IReceivableRepository, ReceivableRepository>();
        services.AddSingleton<IImportRecordStore, InMemoryImportRecordStore>();
        services.AddSingleton<IExcelImportService, ExcelImportService>();
        services.AddSingleton<IExcelConvertService, ExcelConvertService>();
        services.AddSingleton<IExcelExportService, ExcelExportService>();
        services.AddSingleton<ReceivableExcelExportService>();
        services.AddSingleton<ICustomerTreeChangedNotifier, CustomerTreeChangedNotifier>();

        return services;
    }

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ReceivableAppService>();
        services.AddScoped<AccountAppService>();
        return services;
    }

    public static void EnsureDatabaseCreated(this IServiceProvider provider, string dbPath)
    {
        var options = provider.GetRequiredService<DbContextOptions<PlErpDbContext>>();
        DbMigrator.EnsureMigrated(options);
    }
}