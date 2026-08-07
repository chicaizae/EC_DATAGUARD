using System.Diagnostics;
using System.Text.Json;
using EcDataguard.Contracts.Agent;
using EcDataguard.Contracts.Common;

namespace EcDataguard.Agent;

public class CommandExecutor
{
    private readonly AgentConfigStore _store;
    private readonly AgentConfig _config;

    public CommandExecutor(AgentConfigStore store, AgentConfig config)
    {
        _store = store;
        _config = config;
    }

    public Task<(AgentCommandStatus Status, string? Note)> ExecuteAsync(AgentCommand command, CancellationToken ct)
    {
        try
        {
            (AgentCommandStatus Status, string? Note) result;
            switch (command.Type)
            {
                case AgentCommandType.ApplyPolicy:
                    result = ApplyPolicy(command);
                    break;
                case AgentCommandType.SetConfig:
                    result = SetConfig(command);
                    break;
                case AgentCommandType.RestartAgent:
                    result = RestartAgent();
                    break;
                case AgentCommandType.UpdateAgent:
                    result = (AgentCommandStatus.Failed, "Aún no se implementa auto-actualización; requerirá un canal de despliegue (fase 2).");
                    break;
                case AgentCommandType.QuarantineDevice:
                    result = Quarantine();
                    break;
                case AgentCommandType.RefreshInventory:
                    result = (AgentCommandStatus.Succeeded, "Inventario refrescado en el siguiente ciclo.");
                    break;
                default:
                    result = (AgentCommandStatus.Failed, $"Mandato desconocido: {command.Type}");
                    break;
            }
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult<(AgentCommandStatus, string?)>((AgentCommandStatus.Failed, ex.Message));
        }
    }

    private (AgentCommandStatus, string?) ApplyPolicy(AgentCommand command)
    {
        using var doc = JsonDocument.Parse(command.PayloadJson ?? "{}");
        var root = doc.RootElement;
        if (root.TryGetProperty("policySetVersion", out var version))
        {
            _config.PolicySetVersion = version.GetInt32();
            _store.Save(_config);
        }
        if (root.TryGetProperty("policies", out var policies))
        {
            _store.SavePolicies(policies.GetRawText());
        }
        return (AgentCommandStatus.Succeeded, $"Políticas aplicadas (versión {_config.PolicySetVersion}).");
    }

    private (AgentCommandStatus, string?) SetConfig(AgentCommand command)
    {
        using var doc = JsonDocument.Parse(command.PayloadJson ?? "{}");
        var root = doc.RootElement;
        if (root.TryGetProperty("heartbeatIntervalSeconds", out var interval) && interval.TryGetInt32(out var seconds))
        {
            _config.HeartbeatIntervalSeconds = Math.Clamp(seconds, 10, 600);
        }
        if (root.TryGetProperty("collectDatabases", out var collect))
        {
            _config.CollectDatabases = collect.GetBoolean();
        }
        if (root.TryGetProperty("scanNetwork", out var scan))
        {
            _config.ScanNetwork = scan.GetBoolean();
        }
        _store.Save(_config);
        return (AgentCommandStatus.Succeeded, "Configuración del agente actualizada.");
    }

    private (AgentCommandStatus, string?) RestartAgent()
    {
        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo("shutdown", "/r /t 5")
            {
                WindowStyle = ProcessWindowStyle.Hidden
            });
            return (AgentCommandStatus.Succeeded, "Reinicio del equipo programado.");
        }
        if (OperatingSystem.IsLinux() && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SYSTEMD_AGENT")))
        {
            Process.Start("systemctl", "restart ecdataguard-agent");
            return (AgentCommandStatus.Succeeded, "Servicio reiniciado.");
        }
        return (AgentCommandStatus.Failed, "Reinicio no soportado en este contexto.");
    }

    private (AgentCommandStatus, string?) Quarantine()
    {
        var marker = Path.Combine(AgentEnvironment.DataDirectory, "quarantined");
        Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
        File.WriteAllText(marker, $"Quarantined {DateTime.UtcNow:O}");
        return (AgentCommandStatus.Succeeded, "Equipo marcado como cuarentena.");
    }
}