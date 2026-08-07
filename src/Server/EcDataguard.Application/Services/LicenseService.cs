using EcDataguard.Application.Abstractions;
using EcDataguard.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EcDataguard.Application.Services;

public interface ILicenseService
{
    Task<TenantLicenseSnapshot?> GetSnapshotAsync(Guid tenantId, CancellationToken ct);
}

public sealed record TenantLicenseSnapshot(
    Guid TenantId,
    TenantPlan Plan,
    int LicensedUserLimit,
    int ActiveUsers,
    int LicensedUsers,
    int Devices,
    bool OverLimit,
    decimal UsagePercent);

public sealed class LicenseService : ILicenseService
{
    private static readonly TimeSpan ActiveWindow = TimeSpan.FromDays(30);
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public LicenseService(IAppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<TenantLicenseSnapshot?> GetSnapshotAsync(Guid tenantId, CancellationToken ct)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return null;

        var cutoff = _clock.UtcNow - ActiveWindow;
        var limit = LicenseRules.UserLimit(tenant.Plan);
        var active = await _db.EndpointUsers.CountAsync(u =>
            u.TenantId == tenantId && u.Licensed && u.LastActivityUtc >= cutoff, ct);
        var licensed = await _db.EndpointUsers.CountAsync(u => u.TenantId == tenantId && u.Licensed, ct);
        var devices = await _db.Devices.CountAsync(d => d.TenantId == tenantId && !d.Deleted, ct);

        return new TenantLicenseSnapshot(
            tenant.Id,
            tenant.Plan,
            limit,
            active,
            licensed,
            devices,
            LicenseRules.IsOverLimit(active, limit),
            LicenseRules.UsagePercent(active, limit));
    }
}

public static class LicenseRules
{
    public static int UserLimit(TenantPlan plan)
        => plan switch
        {
            TenantPlan.Standard => 25,
            TenantPlan.Premium => 250,
            TenantPlan.Enterprise => 2000,
            _ => 25
        };

    public static bool IsOverLimit(int activeUsers, int limit)
        => limit > 0 && activeUsers > limit;

    public static decimal UsagePercent(int activeUsers, int limit)
        => limit <= 0 ? 0 : Math.Round(activeUsers * 100m / limit, 2);
}
