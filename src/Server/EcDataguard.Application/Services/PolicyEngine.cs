using EcDataguard.Domain.Enums;
using EcDataguard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using EcDataguard.Application.Abstractions;

namespace EcDataguard.Application.Services;

public interface IPolicyEngine
{
    Task<IReadOnlyList<Policy>> GetEnabledAsync(Guid tenantId, CancellationToken ct);
}

public sealed class PolicyEngine : IPolicyEngine
{
    private readonly IAppDbContext _db;

    public PolicyEngine(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Policy>> GetEnabledAsync(Guid tenantId, CancellationToken ct)
        => await _db.Policies
            .Where(p => p.TenantId == tenantId && p.Enabled && p.Kind == PolicyKind.Data)
            .OrderBy(p => p.Priority)
            .ToListAsync(ct);
}

public static class PolicyEvaluator
{
    public static Policy? FirstMatch(IEnumerable<Policy> orderedPolicies, EventRecord draft, IReadOnlyList<string> classifications)
    {
        foreach (var policy in orderedPolicies)
        {
            if (ConditionsApply(policy, draft, classifications))
                return policy;
        }
        return null;
    }

    private static bool ConditionsApply(Policy p, EventRecord draft, IReadOnlyList<string> classifications)
    {
        var conditions = Deserialize(p.ConditionsJson);
        if (conditions is null) return true;

        if (conditions.Classifications is { Count: > 0 } wanted)
        {
            var have = classifications as IReadOnlySet<string> ?? classifications.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!wanted.Any(c => have.Contains(c))) return false;
        }

        if (conditions.Destinations is { Count: > 0 } dests)
        {
            var destType = draft.DestinationType ?? string.Empty;
            if (!dests.Any(d => string.Equals(d, destType, StringComparison.OrdinalIgnoreCase))) return false;
        }

        return true;
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };

    private static PolicyConditionsJson? Deserialize(string json)
    {
        try { return System.Text.Json.JsonSerializer.Deserialize<PolicyConditionsJson>(json, JsonOptions); }
        catch { return null; }
    }
}

public record PolicyConditionsJson
{
    public List<string> Destinations { get; set; } = new();
    public List<string> Classifications { get; set; } = new();
}

public record PolicyEvaluationResult(Policy? Policy, bool Matched, EventRecord Draft);