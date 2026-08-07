using EcDataguard.Domain.Entities;
using EcDataguard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using EcDataguard.Application.Abstractions;

namespace EcDataguard.Application.Services;

public interface IEventIngestionService
{
    Task<(int Accepted, int Rejected)> IngestAsync(Guid tenantId, Guid deviceId, IReadOnlyList<EventInput> events, CancellationToken ct);
}

public sealed record EventInput(
    string ExternalId,
    EventKind Kind,
    DateTime OccurredUtc,
    string? UserName,
    string? ProcessName,
    string? Operation,
    string? FilePath,
    string? DestinationType,
    string? DestinationDetail,
    long FileSizeBytes,
    string? FileHash,
    IReadOnlyList<string> Classifications,
    DbEngine? DbEngine,
    string? DbHost,
    int? DbPort,
    string? Detail);

public sealed class EventIngestionService : IEventIngestionService
{
    private readonly IAppDbContext _db;
    private readonly IPolicyEngine _policyEngine;
    private readonly ISiemGateway _siem;
    private readonly IClock _clock;
    private readonly IAdminTrailService _adminTrail;
    private readonly IClassificationService _classificationService;

    public EventIngestionService(
        IAppDbContext db,
        IPolicyEngine policyEngine,
        ISiemGateway siem,
        IClock clock,
        IAdminTrailService adminTrail,
        IClassificationService classificationService)
    {
        _db = db;
        _policyEngine = policyEngine;
        _siem = siem;
        _clock = clock;
        _adminTrail = adminTrail;
        _classificationService = classificationService;
    }

    public async Task<(int Accepted, int Rejected)> IngestAsync(Guid tenantId, Guid deviceId, IReadOnlyList<EventInput> events, CancellationToken ct)
    {
        if (events.Count == 0) return (0, 0);

        var policies = await _policyEngine.GetEnabledAsync(tenantId, ct);
        var now = _clock.UtcNow;
        int accepted = 0;

        var externalIds = events.Select(e => e.ExternalId).ToList();
        var existingIds = await _db.Events
            .Where(e => e.TenantId == tenantId && externalIds.Contains(e.ExternalId))
            .Select(e => e.ExternalId)
            .ToListAsync(ct);
        var existingSet = new HashSet<string>(existingIds, StringComparer.Ordinal);

        foreach (var input in events)
        {
            if (!existingSet.Add(input.ExternalId))
            {
                continue;
            }

            var classifications = input.Classifications
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var fileType = _classificationService.DetectFileType(input.FilePath);
            if (!string.IsNullOrWhiteSpace(fileType)
                && !classifications.Contains($"file:{fileType}", StringComparer.OrdinalIgnoreCase))
            {
                classifications.Add($"file:{fileType}");
            }

            var draft = new EventRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DeviceId = deviceId,
                ExternalId = input.ExternalId,
                Kind = input.Kind,
                OccurredUtc = input.OccurredUtc,
                IngestedUtc = now,
                UserName = input.UserName,
                ProcessName = input.ProcessName,
                Operation = input.Operation,
                FilePath = input.FilePath,
                DestinationType = input.DestinationType,
                DestinationDetail = input.DestinationDetail,
                FileSizeBytes = input.FileSizeBytes,
                FileHash = input.FileHash,
                Classifications = string.Join('|', classifications),
                DbEngine = input.DbEngine?.ToString(),
                DbHost = input.DbHost,
                DbPort = input.DbPort,
                Detail = input.Detail
            };

            var policy = PolicyEvaluator.FirstMatch(policies, draft, classifications);
            if (policy is not null)
            {
                draft.AppliedPolicyId = policy.Id;
                draft.AppliedAction = policy.Action;
                draft.Blocked = policy.Action is PolicyAction.Block or PolicyAction.BlockWithOverride;
            }

            _db.Events.Add(draft);
            await UpsertEndpointUserAsync(tenantId, input.UserName, input.OccurredUtc, ct);
            accepted++;
        }

        await _db.SaveChangesAsync(ct);

        if (accepted > 0)
        {
            await MaybeRaiseInsightsAsync(tenantId, ct);
            _ = Task.Run(() => _adminTrail.RecordAsync(tenantId, null, "Events", "Ingest", "Ingested " + accepted + " events", "{}", CancellationToken.None));
        }

        return (accepted, 0);
    }

    private async Task UpsertEndpointUserAsync(Guid tenantId, string? userName, DateTime activityUtc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userName)) return;

        var normalized = userName.Trim();
        var user = await _db.EndpointUsers.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.UserName == normalized, ct);
        if (user is null)
        {
            _db.EndpointUsers.Add(new EndpointUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserName = normalized,
                Licensed = true,
                LastActivityUtc = activityUtc
            });
            return;
        }

        user.Licensed = true;
        if (user.LastActivityUtc is null || user.LastActivityUtc < activityUtc)
        {
            user.LastActivityUtc = activityUtc;
        }
    }

    private async Task MaybeRaiseInsightsAsync(Guid tenantId, CancellationToken ct)
    {
        var blocked = await _db.Events
            .Where(e => e.TenantId == tenantId && e.Blocked)
            .OrderByDescending(e => e.IngestedUtc)
            .Take(50).ToListAsync(ct);

        var raised = new List<Insight>();

        foreach (var ev in blocked)
        {
            var insight = new Insight
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Severity = InsightSeverity.High,
                Status = InsightStatus.Open,
                Reason = "Blocked by policy",
                SummaryJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    eventId = ev.Id,
                    externalId = ev.ExternalId,
                    operation = ev.Operation,
                    filePath = ev.FilePath,
                    destinationType = ev.DestinationType,
                    classifications = ev.Classifications
                }),
                RelatedEventCount = 1,
                LastActivityUtc = ev.IngestedUtc,
                CreatedUtc = _clock.UtcNow
            };
            await _db.Insights.AddAsync(insight, ct);
            raised.Add(insight);
        }
        await _db.SaveChangesAsync(ct);

        if (_siem.Enabled)
        {
            foreach (var insight in raised)
            {
                var payload = SiemPayloadBuilder.Insight(insight);
                var result = await _siem.DeliverAsync(tenantId, payload, ct);
                _db.SiemDeliveryLogs.Add(new SiemDeliveryLog
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Target = result.Target,
                    PayloadJson = payload,
                    SentUtc = _clock.UtcNow,
                    Success = result.Success,
                    Error = result.Error
                });
            }
            await _db.SaveChangesAsync(ct);
        }
    }
}
