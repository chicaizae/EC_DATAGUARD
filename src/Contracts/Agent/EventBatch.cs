using System;
using System.Collections.Generic;
using EcDataguard.Contracts.Common;

namespace EcDataguard.Contracts.Agent;

public class ActorInfo
{
    public string? UserName { get; set; }
    public string? ProcessName { get; set; }
    public int Pid { get; set; }
}

public class ContentScanResult
{
    public bool Done { get; set; }
    public List<string> Classifications { get; set; } = new();
}

public class EventReport
{
    public string EventId { get; set; } = string.Empty;
    public EventKind Kind { get; set; }
    public DateTime OccurredUtc { get; set; }
    public ActorInfo? Actor { get; set; }
    public string? Operation { get; set; }
    public string? FilePath { get; set; }
    public string? DestinationType { get; set; }
    public string? DestinationDetail { get; set; }
    public long FileSizeBytes { get; set; }
    public string? FileHashSha256 { get; set; }
    public ContentScanResult? ContentScan { get; set; }

    public DbArtifactInfo? DbArtifact { get; set; }
    public string? Detail { get; set; }
}

public class EventBatchRequest
{
    public List<EventReport> Events { get; set; } = new();
}

public class EventBatchResponse
{
    public int Accepted { get; set; }
    public int Rejected { get; set; }
    public DateTime NextUploadAllowedUtc { get; set; }
}