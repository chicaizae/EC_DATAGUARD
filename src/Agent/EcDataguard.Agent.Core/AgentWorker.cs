using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EcDataguard.Contracts.Agent;
using EcDataguard.Contracts.Common;

namespace EcDataguard.Agent;

public class AgentWorker : BackgroundService
{
    private readonly AgentConfigStore _store;
    private readonly AgentConfig _config;
    private readonly DatabaseDiscovery _discovery;
    private readonly AgentClient _client;
    private readonly CommandExecutor _executor;
    private readonly ILogger<AgentWorker> _logger;
    private readonly HashSet<string> _knownDatabases = new(StringComparer.OrdinalIgnoreCase);
    private int _delayedFailures = 0;

    public AgentWorker(
        AgentConfigStore store,
        DatabaseDiscovery discovery,
        AgentClient client,
        CommandExecutor executor,
        ILogger<AgentWorker> logger)
    {
        _store = store;
        _config = store.Load();
        _discovery = discovery;
        _client = client;
        _executor = executor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EC DATAGUARD agent v{Version} ({Os})", AgentEnvironment.AgentVersion, AgentEnvironment.CurrentOs);

        if (string.IsNullOrWhiteSpace(_config.ServerUrl))
        {
            _logger.LogError("No se configuró ServerUrl. Use --server http://<host>:8080/api");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_config.DeviceToken))
                {
                    _logger.LogWarning("Token no configurado. Genere el instalador en Consola > Dispositivos.");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }

                await HeartbeatAndCommandsAsync(stoppingToken);
                _delayedFailures = 0;
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogError("Token rechazado; el dispositivo fue desvinculado o revocado.");
                _config.DeviceToken = string.Empty;
                _store.Save(_config);
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (Exception ex)
            {
                _delayedFailures++;
                var delay = TimeSpan.FromSeconds(Math.Min(60, 5 * _delayedFailures));
                _logger.LogWarning("Heartbeat falló: {Message}. Reintento en {Seconds}s.", ex.Message, delay.TotalSeconds);
                try { await Task.Delay(delay, stoppingToken); }
                catch (TaskCanceledException) { break; }
            }
        }
    }

    private async Task HeartbeatAndCommandsAsync(CancellationToken ct)
    {
        var databases = _config.CollectDatabases
            ? _discovery.Scan()
            : Array.Empty<DbArtifactInfo>();

        var heartbeat = new HeartbeatRequest
        {
            DeviceId = _config.DeviceId,
            Os = AgentEnvironment.CurrentOs,
            OsVersion = Environment.OSVersion.VersionString,
            Hostname = AgentEnvironment.Hostname,
            UserName = AgentEnvironment.CurrentUser,
            AgentVersion = AgentEnvironment.AgentVersion,
            UptimeSeconds = AgentEnvironment.UptimeSeconds,
            ProtectionState = ProtectionState.Protected,
            Databases = databases.ToList()
        };

        var response = await _client.HeartbeatAsync(heartbeat, ct);

        await ApplyServerConfigurationAsync(response.Config);

        if (response.Commands != null)
        {
            foreach (var command in response.Commands)
            {
                var (status, note) = await _executor.ExecuteAsync(command, ct);
                await _client.AckAsync(new AgentCommandAck
                {
                    CommandId = command.CommandId,
                    Status = status,
                    Detail = note
                }, ct);
            }
        }

        await SendNewDatabaseEventsAsync(databases, ct);
    }

    private Task ApplyServerConfigurationAsync(ServerRuntimeConfig? config)
    {
        if (config == null) return Task.CompletedTask;
        if (config.PolicySetVersion != _config.PolicySetVersion)
        {
            _config.PolicySetVersion = config.PolicySetVersion;
            _store.Save(_config);
        }
        return Task.CompletedTask;
    }

    private async Task SendNewDatabaseEventsAsync(IReadOnlyList<DbArtifactInfo> databases, CancellationToken ct)
    {
        if (databases.Count == 0) return;

        var novel = databases.Where(d => _knownDatabases.Add(Key(d))).ToList();
        if (novel.Count == 0) return;

        var events = novel.Select(d => new EventReport
        {
            EventId = Guid.NewGuid().ToString("N"),
            OccurredUtc = DateTime.UtcNow,
            Kind = EventKind.DbFound,
            Actor = new ActorInfo { UserName = AgentEnvironment.CurrentUser, ProcessName = "ecdataguard-agent" },
            Operation = "db_found",
            DestinationType = d.Engine.ToString(),
            DbArtifact = d
        }).ToList();

        var batch = new EventBatchRequest { Events = events };

        var ack = await _client.SendEventsAsync(batch, ct);
        _logger.LogInformation("BD detectadas: {Engine}. Aceptados {Accepted} / rechazados {Rejected}",
            string.Join(", ", novel.Select(n => n.Engine)), ack.Accepted, ack.Rejected);
    }

    private static string Key(DbArtifactInfo db)
        => $"{db.Engine}|{db.Host}|{db.Port}|{db.Instance}";
}