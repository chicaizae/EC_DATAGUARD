using System.Text.Json;
using EcDataguard.Domain.Entities;

namespace EcDataguard.Application.Services;

public static class SiemPayloadBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string AdminAction(AdminAction a)
        => JsonSerializer.Serialize(new
        {
            activity_name = "Admin Activity",
            category_name = "Application Activity",
            class_name = "Application Activity",
            metadata = new
            {
                product = new { name = "EC DATAGUARD" },
                version = "1.0.0",
                profiles = new[] { "ocsf" }
            },
            time = new DateTimeOffset(a.OccurredUtc).ToUnixTimeMilliseconds(),
            type_name = "EC DATAGUARD: Admin Activity",
            tenant_uid = a.TenantId,
            actor = new
            {
                user = new
                {
                    uid = a.ActorUserId,
                    name = a.ActorName
                }
            },
            api = new
            {
                operation = a.Activity,
                service = new { name = a.Section }
            },
            unmapped = new
            {
                admin_action_id = a.Id,
                content = a.ContentJson
            }
        }, JsonOptions);

    public static string Insight(Insight insight)
        => JsonSerializer.Serialize(new
        {
            activity_name = "Security Finding",
            category_name = "Findings",
            class_name = "Security Finding",
            metadata = new
            {
                product = new { name = "EC DATAGUARD" },
                version = "1.0.0",
                profiles = new[] { "ocsf" }
            },
            time = new DateTimeOffset(insight.CreatedUtc).ToUnixTimeMilliseconds(),
            type_name = "EC DATAGUARD: Security Finding",
            severity = insight.Severity.ToString(),
            status = insight.Status.ToString(),
            tenant_uid = insight.TenantId,
            finding = new
            {
                uid = insight.Id,
                title = insight.Reason,
                desc = insight.SummaryJson,
                last_seen_time = insight.LastActivityUtc.HasValue
                    ? new DateTimeOffset(insight.LastActivityUtc.Value).ToUnixTimeMilliseconds()
                    : (long?)null,
                related_events = insight.RelatedEventCount
            }
        }, JsonOptions);
}
