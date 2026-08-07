using EcDataguard.Domain.Entities;
using EcDataguard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using EcDataguard.Application.Abstractions;

namespace EcDataguard.Application.Services;

public interface IConsoleQueryService
{
    Task<IReadOnlyList<Device>> GetDevicesAsync(Guid? tenantId, CancellationToken ct);
    Task<IReadOnlyList<EventRecord>> GetEventsAsync(Guid? tenantId, int limit, CancellationToken ct);
    Task<EventRecord?> GetEventAsync(Guid tenantId, Guid eventId, CancellationToken ct);
    Task<IReadOnlyList<Insight>> GetInsightsAsync(Guid? tenantId, CancellationToken ct);
    Task<IReadOnlyList<DeviceDbArtifact>> GetDbArtifactsAsync(Guid? tenantId, CancellationToken ct);
    Task<IReadOnlyList<Policy>> GetPoliciesAsync(Guid? tenantId, CancellationToken ct);
    Task<IReadOnlyList<AdminAction>> GetAdminTrailAsync(Guid? tenantId, int limit, CancellationToken ct);
    Task CloseInsightAsync(Guid tenantId, Guid insightId, string reason, Guid? closedBy, CancellationToken ct);
}

public sealed class ConsoleQueryService : IConsoleQueryService
{
    private readonly IAppDbContext _db;

    public ConsoleQueryService(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Device>> GetDevicesAsync(Guid? tenantId, CancellationToken ct)
        => await _db.Devices.Where(d => (tenantId == null || d.TenantId == tenantId) && !d.Deleted)
            .OrderByDescending(d => d.LastHeartbeatUtc).ToListAsync(ct);

    public async Task<IReadOnlyList<EventRecord>> GetEventsAsync(Guid? tenantId, int limit, CancellationToken ct)
        => await _db.Events.Where(e => tenantId == null || e.TenantId == tenantId)
            .OrderByDescending(e => e.IngestedUtc).Take(limit).ToListAsync(ct);

    public async Task<EventRecord?> GetEventAsync(Guid tenantId, Guid eventId, CancellationToken ct)
        => await _db.Events.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == eventId, ct);

    public async Task<IReadOnlyList<Insight>> GetInsightsAsync(Guid? tenantId, CancellationToken ct)
        => await _db.Insights.Where(i => tenantId == null || i.TenantId == tenantId)
            .OrderByDescending(i => i.LastActivityUtc).ToListAsync(ct);

    public async Task<IReadOnlyList<DeviceDbArtifact>> GetDbArtifactsAsync(Guid? tenantId, CancellationToken ct)
        => await _db.DeviceDbArtifacts.Where(a => tenantId == null || a.TenantId == tenantId)
            .OrderByDescending(a => a.DiscoveredUtc).ToListAsync(ct);

    public async Task<IReadOnlyList<Policy>> GetPoliciesAsync(Guid? tenantId, CancellationToken ct)
        => await _db.Policies.Where(p => tenantId == null || p.TenantId == tenantId)
            .OrderBy(p => p.Priority).ToListAsync(ct);

    public async Task<IReadOnlyList<AdminAction>> GetAdminTrailAsync(Guid? tenantId, int limit, CancellationToken ct)
        => await _db.AdminActions.Where(a => tenantId == null || a.TenantId == tenantId)
            .OrderByDescending(a => a.OccurredUtc).Take(limit).ToListAsync(ct);

    public async Task CloseInsightAsync(Guid tenantId, Guid insightId, string reason, Guid? closedBy, CancellationToken ct)
    {
        var insight = await _db.Insights.FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Id == insightId, ct);
        if (insight is null) return;
        insight.Status = InsightStatus.Closed;
        insight.ClosureReason = reason;
        insight.ClosedByUserId = closedBy;
        insight.ClosedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
