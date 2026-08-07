using EcDataguard.Domain.Entities;
using EcDataguard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using EcDataguard.Application.Abstractions;

namespace EcDataguard.Application.Services;

public interface IDashboardService
{
    Task<DashboardSnapshot> GetAsync(Guid? tenantId, CancellationToken ct);
}

public sealed record DashboardSnapshot(
    int TenantCount,
    int DeviceCount,
    int OnlineDevices,
    int ProtectedDevices,
    int DegradedDevices,
    int UnprotectedDevices,
    int OpenInsights,
    int HighInsights,
    int BlockedEvents,
    int TotalEvents,
    int DbArtifacts);

public sealed class DashboardService : IDashboardService
{
    private readonly IAppDbContext _db;

    public DashboardService(IAppDbContext db) => _db = db;

    public async Task<DashboardSnapshot> GetAsync(Guid? tenantId, CancellationToken ct)
    {
        var tenants = tenantId.HasValue
            ? _db.Tenants.Where(t => t.Id == tenantId.Value)
            : _db.Tenants;

        var devicesQuery = tenantId.HasValue
            ? _db.Devices.Where(d => d.TenantId == tenantId.Value && !d.Deleted)
            : _db.Devices.Where(d => !d.Deleted);

        var eventsQuery = tenantId.HasValue
            ? _db.Events.Where(e => e.TenantId == tenantId.Value)
            : _db.Events;

        var tenantsCount = await tenants.CountAsync(ct);
        var devices = await devicesQuery.ToListAsync(ct);
        var now = DateTime.UtcNow;

        int online = 0, protectedCount = 0, degradedCount = 0, unprotectedCount = 0;
        foreach (var d in devices)
        {
            if (now - d.LastHeartbeatUtc.GetValueOrDefault() < TimeSpan.FromMinutes(5)) online++;
            protectedCount += d.ProtectionState == ProtectionState.Protected ? 1 : 0;
            degradedCount += d.ProtectionState == ProtectionState.Degraded ? 1 : 0;
            unprotectedCount += d.ProtectionState == ProtectionState.Unprotected ? 1 : 0;
        }

        var openInsights = await _db.Insights.CountAsync(i => i.TenantId == (tenantId ?? i.TenantId) && i.Status == InsightStatus.Open, ct);
        var highInsights = await _db.Insights.CountAsync(i => i.TenantId == (tenantId ?? i.TenantId) && i.Status == InsightStatus.Open && i.Severity >= InsightSeverity.High, ct);

        var blocked = await eventsQuery.CountAsync(e => e.Blocked, ct);
        var total = await eventsQuery.CountAsync(ct);
        var dbArtifacts = tenantId.HasValue
            ? await _db.DeviceDbArtifacts.CountAsync(a => a.TenantId == tenantId.Value, ct)
            : await _db.DeviceDbArtifacts.CountAsync(ct);

        return new DashboardSnapshot(
            tenantsCount,
            devices.Count,
            online,
            protectedCount,
            degradedCount,
            unprotectedCount,
            openInsights,
            highInsights,
            blocked,
            total,
            dbArtifacts);
    }
}