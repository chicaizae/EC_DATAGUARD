using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EcDataguard.Application.Services;

namespace EcDataguard.Infrastructure.Reporting;

/// <summary>
/// Revisa cada minuto si hay reportes programados vencidos y los envía por correo.
/// </summary>
public sealed class ReportSchedulerHost : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReportSchedulerHost> _logger;

    public ReportSchedulerHost(IServiceScopeFactory scopeFactory, ILogger<ReportSchedulerHost> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Planificador de reportes iniciado (tick {Tick}).", Tick);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Tick, stoppingToken);
                using var scope = _scopeFactory.CreateScope();
                var reports = scope.ServiceProvider.GetRequiredService<IReportService>();
                var sent = await reports.RunDueAsync(stoppingToken);
                if (sent > 0)
                {
                    _logger.LogInformation("Reportes enviados en este ciclo: {Count}.", sent);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("El ciclo de reportes falló: {Message}", ex.Message);
            }
        }
    }
}