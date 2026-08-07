using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EcDataguard.Application.Abstractions;
using EcDataguard.Application.Services;
using EcDataguard.Infrastructure.EfCore;
using EcDataguard.Infrastructure.Integrations;
using EcDataguard.Infrastructure.Security;

namespace EcDataguard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "Postgres";
        services.AddDbContext<AppDbContext>(options =>
        {
            if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                var sqlite = configuration.GetConnectionString("Sqlite") ?? "Data Source=data/ecdataguard-dev.db";
                var path = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(sqlite).DataSource;
                var directory = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                options.UseSqlite(sqlite);
                return;
            }

            var postgres = configuration.GetConnectionString("Postgres")
                ?? "Host=localhost;Port=5432;Database=ecdataguard;Username=ecdataguard;Password=ecdataguard";
            options.UseNpgsql(postgres, npgsql => npgsql.EnableRetryOnFailure(3));
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        var jwt = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        services.AddSingleton(jwt);
        services.AddSingleton<ITokenService, JwtTokenService>();

        var siem = configuration.GetSection("Siem").Get<SiemOptions>() ?? new SiemOptions();
        services.AddSingleton(siem);
        services.AddHttpClient("siem");
        services.AddSingleton<ISiemGateway, HttpSiemGateway>();

        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<ITenantAdminService, TenantAdminService>();
        services.AddScoped<IDeviceAgentService, DeviceAgentService>();
        services.AddScoped<IPolicyEngine, PolicyEngine>();
        services.AddScoped<IClassificationService, ClassificationService>();
        services.AddScoped<IEventIngestionService, EventIngestionService>();
        services.AddScoped<IAdminTrailService, AdminTrailService>();
        services.AddScoped<ILicenseService, LicenseService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IConsoleQueryService, ConsoleQueryService>();
        services.AddScoped<ICommandService, CommandService>();

        return services;
    }
}
