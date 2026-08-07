using EcDataguard.Domain.Enums;

namespace EcDataguard.Domain.Entities;

public class EventRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DeviceId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public EventKind Kind { get; set; }
    public DateTime OccurredUtc { get; set; }
    public DateTime IngestedUtc { get; set; } = DateTime.UtcNow;
    public string? UserName { get; set; }
    public string? ProcessName { get; set; }
    public string? Operation { get; set; }
    public string? FilePath { get; set; }
    public string? DestinationType { get; set; }
    public string? DestinationDetail { get; set; }
    public long FileSizeBytes { get; set; }
    public string? FileHash { get; set; }
    public string? Classifications { get; set; }
    public string? DbEngine { get; set; }
    public string? DbHost { get; set; }
    public int? DbPort { get; set; }
    public string? Detail { get; set; }

    public Guid? AppliedPolicyId { get; set; }
    public PolicyAction? AppliedAction { get; set; }
    public bool Blocked { get; set; }
}

public class Insight
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public InsightSeverity Severity { get; set; }
    public InsightStatus Status { get; set; } = InsightStatus.Open;
    public string Reason { get; set; } = string.Empty;
    public string SummaryJson { get; set; } = "{}";
    public int RelatedEventCount { get; set; }
    public DateTime? LastActivityUtc { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string? ClosureReason { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public DateTime? ClosedUtc { get; set; }
}

public class AdminAction
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string Activity { get; set; } = string.Empty;
    public string ContentJson { get; set; } = "{}";
    public DateTime OccurredUtc { get; set; } = DateTime.UtcNow;
}

public class SiemDeliveryLog
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string Target { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DateTime SentUtc { get; set; } = DateTime.UtcNow;
    public bool Success { get; set; }
    public string? Error { get; set; }
}