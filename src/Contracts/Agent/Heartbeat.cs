using System;
using System.Collections.Generic;
using EcDataguard.Contracts.Common;

namespace EcDataguard.Contracts.Agent;

public class NetworkInterfaceInfo
{
    public string? Name { get; set; }
    public string? Ip { get; set; }
    public string? Mac { get; set; }
}

public class DbArtifactInfo
{
    public DbEngine Engine { get; set; }
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; }
    public string? Instance { get; set; }
    public bool Reachable { get; set; }
}

public class SophosCompatibilityInfo
{
    public bool XdrCompatible { get; set; }
    public bool TrustHashRegistered { get; set; }
}

public class HeartbeatRequest
{
    public Guid DeviceId { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public OsFamily Os { get; set; }
    public string? OsVersion { get; set; }
    public string AgentVersion { get; set; } = "1.0.0";
    public ProtectionState ProtectionState { get; set; }
    public List<string> ProtectionDetails { get; set; } = new();
    public int ConfigRevision { get; set; }
    public string? UserName { get; set; }
    public List<NetworkInterfaceInfo> NetworkInterfaces { get; set; } = new();
    public List<DbArtifactInfo> Databases { get; set; } = new();
    public SophosCompatibilityInfo? Sophos { get; set; }
    public long UptimeSeconds { get; set; }
    public bool Test { get; set; }
}

public class ServerRuntimeConfig
{
    public int PolicySetVersion { get; set; }
    public int HeartbeatIntervalSeconds { get; set; }
    public bool CollectDatabases { get; set; } = true;

    /// <summary>Políticas activas del tenant para evaluación local en el agente.</summary>
    public Contracts.Policies.PolicySet? PolicySet { get; set; }
}

public class HeartbeatResponse
{
    public DateTime ServerTimeUtc { get; set; }
    public List<AgentCommand> Commands { get; set; } = new();
    public ServerRuntimeConfig? Config { get; set; }
}

public class AgentCommand
{
    public string CommandId { get; set; } = string.Empty;
    public AgentCommandType Type { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public DateTime IssuedUtc { get; set; }
}

public class AgentCommandAck
{
    public string CommandId { get; set; } = string.Empty;
    public AgentCommandStatus Status { get; set; }
    public string? Detail { get; set; }
    public DateTime AppliedUtc { get; set; }
}