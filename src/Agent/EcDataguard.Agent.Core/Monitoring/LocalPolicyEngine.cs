using EcDataguard.Contracts.Agent;
using EcDataguard.Contracts.Common;
using EcDataguard.Contracts.Policies;

namespace EcDataguard.Agent.Monitoring;

/// <summary>
/// Evaluación de políticas local (Windows/Linux) sobre eventos capturados por los
/// monitores DLP. Reproduce la semántica del motor del servidor: se evalúan las
/// políticas habilitadas ordenadas por prioridad y aplica la primera coincidencia
/// (clasificaciones o destinos listados = restricción "al menos uno").
/// </summary>
public static class LocalPolicyEngine
{
    public static PolicyDescriptor? FindFirstMatch(IReadOnlyList<PolicyDescriptor> policies, EventReport ev)
    {
        foreach (var policy in policies
                     .Where(p => p.Enabled)
                     .OrderBy(p => p.Priority))
        {
            if (Matches(policy, ev))
            {
                return policy;
            }
        }
        return null;
    }

    /// <summary>Aplica la decisión de la política sobre el evento (Block/BlockWithOverride bane Blocked).</summary>
    public static void ApplyPolicy(PolicyDescriptor? policy, EventReport evt)
    {
        if (policy is null)
        {
            evt.PolicyId = null;
            evt.AppliedAction = null;
            evt.Blocked = false;
            return;
        }

        evt.PolicyId = policy.Id;
        evt.AppliedAction = policy.Action;
        evt.Blocked = policy.Action is PolicyEngineAction.Block or PolicyEngineAction.BlockWithOverride;

        if (evt.Blocked)
        {
            evt.Detail = string.IsNullOrWhiteSpace(evt.Detail)
                ? $"Bloqueada por política '{policy.Name}'."
                : $"{evt.Detail} | Bloqueada por política '{policy.Name}'.";
        }
    }

    private static bool Matches(PolicyDescriptor policy, EventReport evt)
    {
        var conditions = policy.Conditions;

        if (conditions.Classifications is { Count: > 0 })
        {
            var evtClassifs = evt.ContentScan?.Classifications
                ?? new List<string>();
            var hasMatch = conditions.Classifications.Any(c =>
                evtClassifs.Contains(c, StringComparer.OrdinalIgnoreCase));
            if (!hasMatch) return false;
        }

        if (conditions.Destinations is { Count: > 0 })
        {
            var destType = evt.DestinationType ?? string.Empty;
            var hasDest = conditions.Destinations.Any(d =>
                string.Equals(d, destType, StringComparison.OrdinalIgnoreCase));
            if (!hasDest) return false;
        }

        return true;
    }
}