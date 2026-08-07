using EcDataguard.Domain.Enums;

namespace EcDataguard.Domain.Entities;

public class Tenant
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public TenantPlan Plan { get; set; } = TenantPlan.Enterprise;
    public bool Activo { get; set; } = true;
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;

    public List<Device> Devices { get; set; } = new();
    public List<Policy> Policies { get; set; } = new();
    public List<Classification> Classifications { get; set; } = new();
    public List<Destination> Destinations { get; set; } = new();
    public List<Team> Teams { get; set; } = new();
    public List<EventRecord> Events { get; set; } = new();
    public List<ConsoleUser> ConsoleUsers { get; set; } = new();
}

public enum TenantPlan
{
    Standard = 0,
    Premium = 1,
    Enterprise = 2
}

public class ConsoleUser
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? TenantScopeOverride { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Role Role { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime? LastSignInUtc { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public class Team
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public string? ExternalSource { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public class EndpointUser
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? TeamRef { get; set; }
    public bool Licensed { get; set; }
    public DateTime? LastActivityUtc { get; set; }
}