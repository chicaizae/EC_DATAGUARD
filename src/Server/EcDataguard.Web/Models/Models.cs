namespace EcDataguard.Web.Models;

public record LoginResult(string Token, Guid UserId, string Email, string Role, Guid TenantId, Guid? ScopeTenantId);

public record DashboardSnapshot(
    int TenantCount,
    int OnlineDevices,
    int ProtectedDevices,
    int DegradedDevices,
    int UnprotectedDevices,
    int OpenInsights,
    int HighInsights,
    int BlockedEvents,
    int TotalEvents,
    int DbArtifacts);

public record TenantDto(
    Guid Id,
    string Codigo,
    string Nombre,
    string Plan,
    bool Activo,
    int Dispositivos,
    int LicensedUserLimit,
    int ActiveUsers,
    int LicensedUsers,
    bool OverLimit,
    decimal UsagePercent);

public record DeviceDto(
    Guid Id,
    string Hostname,
    string Os,
    string? OsVersion,
    string AgentVersion,
    string Protection,
    string? ProtectionDetails,
    DateTime? LastHeartbeatUtc,
    bool Online,
    string? LastUser,
    string? IpAddress,
    DateTime? CreatedUtc);

public record EventDto(
    Guid Id,
    string ExternalId,
    string Kind,
    DateTimeOffset OccurredUtc,
    string? UserName,
    string? ProcessName,
    string? Operation,
    string? FilePath,
    string? DestinationType,
    string? DestinationDetail,
    long FileSizeBytes,
    string? FileHash,
    string? Classifications,
    string? DbEngine,
    string? DbHost,
    int? DbPort,
    string? Detail,
    bool Blocked,
    string? PolicyAction,
    Guid? AppliedPolicyId);

public record PolicyDto(
    Guid Id,
    string Name,
    string Kind,
    bool Enabled,
    int Priority,
    string Action,
    string ConditionsJson,
    int Revision);

public record InsightDto(Guid Id, string Severity, string Status, string Reason, int RelatedEventCount, DateTimeOffset? LastActivityUtc, DateTimeOffset? CreatedUtc);

public record AdminTrailDto(Guid Id, Guid? TenantId, string ActorName, string Section, string Activity, DateTimeOffset OccurredUtc);

public record DbArtifactDto(Guid Id, Guid DeviceId, string Engine, string Host, int Port, string? Instance, bool Reachable, DateTimeOffset DiscoveredUtc);

public record RegisterDeviceRequest(string Hostname, string Os);
public record IssueCommandRequest(string Kind, object? Payload = null);

public record CreateTenantRequest(string Codigo, string Nombre, string Plan);

public record InstallerRequest(string Os);
public record CloseInsightRequest(string? Reason);

public record InstallerInfo(
    string DownloadUrl,
    string FileName,
    string Command,
    string Token);
