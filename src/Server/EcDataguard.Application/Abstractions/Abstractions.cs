using Microsoft.EntityFrameworkCore;
using EcDataguard.Domain.Entities;

namespace EcDataguard.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<ConsoleUser> ConsoleUsers { get; }
    DbSet<Team> Teams { get; }
    DbSet<EndpointUser> EndpointUsers { get; }
    DbSet<Device> Devices { get; }
    DbSet<DeviceDbArtifact> DeviceDbArtifacts { get; }
    DbSet<DeviceToken> DeviceTokens { get; }
    DbSet<AgentCommand> AgentCommands { get; }
    DbSet<Classification> Classifications { get; }
    DbSet<Destination> Destinations { get; }
    DbSet<Policy> Policies { get; }
    DbSet<EventRecord> Events { get; }
    DbSet<Insight> Insights { get; }
    DbSet<AdminAction> AdminActions { get; }
    DbSet<SiemDeliveryLog> SiemDeliveryLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    void SetTenantScope(Guid tenantId);
    Guid? CurrentTenantScope { get; }
}

public interface ITokenService
{
    string IssueConsoleToken(ConsoleUser user, Guid tenantId, Guid? scopeTenantId);
    string IssueDeviceToken(Guid tenantId, Guid deviceId, TimeSpan lifetime);
}

public interface ISiemGateway
{
    bool Enabled { get; }
    Task<DeliveryResult> DeliverAsync(Guid? tenantId, string payloadJson, CancellationToken ct);
}

public record DeliveryResult(bool Success, string? Error, string Target);

public interface IClock
{
    DateTime UtcNow { get; }
}

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}