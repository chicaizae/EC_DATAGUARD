using EcDataguard.Domain.Enums;
using EcDataguard.Contracts.Agent;
using ContractsAgentCommandType = EcDataguard.Contracts.Common.AgentCommandType;
using ContractsEventKind = EcDataguard.Contracts.Common.EventKind;
using ContractsDbEngine = EcDataguard.Contracts.Common.DbEngine;
using ContractsOsFamily = EcDataguard.Contracts.Common.OsFamily;
using ContractsProtectionState = EcDataguard.Contracts.Common.ProtectionState;
using Xunit;

namespace EcDataguard.Tests;

public class ProtocolAlignmentTests
{
    [Fact]
    public void AgentCommandKind_MismoOrdenQueAgentCommandType()
    {
        // El agente convierte (int)c.Kind a AgentCommandType: los valores deben coincidir.
        Assert.Equal((int)AgentCommandKind.ApplyPolicy, (int)ContractsAgentCommandType.ApplyPolicy);
        Assert.Equal((int)AgentCommandKind.SetConfig, (int)ContractsAgentCommandType.SetConfig);
        Assert.Equal((int)AgentCommandKind.RestartAgent, (int)ContractsAgentCommandType.RestartAgent);
        Assert.Equal((int)AgentCommandKind.UpdateAgent, (int)ContractsAgentCommandType.UpdateAgent);
        Assert.Equal((int)AgentCommandKind.QuarantineDevice, (int)ContractsAgentCommandType.QuarantineDevice);
        Assert.Equal((int)AgentCommandKind.RefreshInventory, (int)ContractsAgentCommandType.RefreshInventory);
    }

    [Fact]
    public void EventKind_CubreLosEventosPrincipales()
    {
        Assert.Equal((int)EventKind.FileOp, (int)ContractsEventKind.FileOp);
        Assert.Equal((int)EventKind.Usb, (int)ContractsEventKind.Usb);
        Assert.Equal((int)EventKind.Web, (int)ContractsEventKind.Web);
        Assert.Equal((int)EventKind.App, (int)ContractsEventKind.App);
        Assert.Equal((int)EventKind.DbFound, (int)ContractsEventKind.DbFound);
        Assert.Equal((int)EventKind.ConfigError, (int)ContractsEventKind.ConfigError);
    }

    [Fact]
    public void DbEngine_ContratosCubrenElInventario()
    {
        Assert.Equal((int)DbEngine.MsSql, (int)ContractsDbEngine.MsSql);
        Assert.Equal((int)DbEngine.PostgreSql, (int)ContractsDbEngine.PostgreSql);
        Assert.Equal((int)DbEngine.MySql, (int)ContractsDbEngine.MySql);
        Assert.Equal((int)DbEngine.MariaDb, (int)ContractsDbEngine.MariaDb);
        Assert.Equal((int)DbEngine.Oracle, (int)ContractsDbEngine.Oracle);
        Assert.Equal((int)DbEngine.MongoDb, (int)ContractsDbEngine.MongoDb);
        Assert.Equal((int)DbEngine.Redis, (int)ContractsDbEngine.Redis);
        Assert.Equal((int)DbEngine.Elasticsearch, (int)ContractsDbEngine.Elasticsearch);
    }

    [Fact]
    public void Heartbeat_LlevaDatosDeInventario()
    {
        var heartbeat = new HeartbeatRequest
        {
            DeviceId = Guid.NewGuid(),
            Hostname = "PC-01",
            Os = ContractsOsFamily.Windows,
            ProtectionState = ContractsProtectionState.Protected,
            Databases = new List<DbArtifactInfo>
            {
                new() { Engine = ContractsDbEngine.MsSql, Host = "127.0.0.1", Port = 1433, Reachable = true }
            }
        };

        Assert.Single(heartbeat.Databases);
    }
}