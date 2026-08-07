using EcDataguard.Domain.Enums;

namespace EcDataguard.Domain.Entities;

public class Device
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public OsType Os { get; set; }
    public string? OsVersion { get; set; }
    public string AgentVersion { get; set; } = string.Empty;
    public ProtectionState ProtectionState { get; set; }
    public string ProtectionDetails { get; set; } = string.Empty;
    public int ConfigRevision { get; set; }
    public DateTime? LastHeartbeatUtc { get; set; }
    public DateTime? LastEventUtc { get; set; }
    public string? LastUser { get; set; }
    public string? IpAddress { get; set; }
    public bool Online => LastHeartbeatUtc.HasValue
        && DateTime.UtcNow - LastHeartbeatUtc.Value < TimeSpan.FromMinutes(5);
    public bool Deleted { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public class DeviceDbArtifact
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DeviceId { get; set; }
    public DbEngine Engine { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string? Instance { get; set; }
    public bool Reachable { get; set; }
    public DateTime DiscoveredUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenUtc { get; set; }
}

public class DeviceToken
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DeviceId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime IssuedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresUtc { get; set; }
    public bool Revoked { get; set; }
}

public class AgentCommand
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DeviceId { get; set; }
    public AgentCommandKind Kind { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public CommandState State { get; set; } = CommandState.Pending;
    public DateTime IssuedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AppliedUtc { get; set; }
    public string? ResultDetail { get; set; }
}