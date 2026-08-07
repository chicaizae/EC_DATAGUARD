using EcDataguard.Domain.Enums;

namespace EcDataguard.Domain.Entities;

public class Classification
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public List<ClassificationRule> Rules { get; set; } = new();
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public class ClassificationRule
{
    public Guid Id { get; set; }
    public Guid ClassificationId { get; set; }
    public RuleType Type { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public bool IsRegex { get; set; }
}

public enum RuleType
{
    Content = 0,
    FileType = 1,
    Entity = 2
}

public class Destination
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Value { get; set; }
    public DestinationTier Tier { get; set; } = DestinationTier.Unassigned;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public enum DestinationTier
{
    Unassigned = 0,
    Safe = 1,
    Untrusted = 2
}

public class Policy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PolicyKind Kind { get; set; }
    public bool Enabled { get; set; }
    public int Priority { get; set; }
    public PolicyAction Action { get; set; }
    public string ScopeJson { get; set; } = "{}";
    public string ConditionsJson { get; set; } = "{}";
    public string InsightTrigger { get; set; } = "Default";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public int Revision { get; set; } = 1;
}