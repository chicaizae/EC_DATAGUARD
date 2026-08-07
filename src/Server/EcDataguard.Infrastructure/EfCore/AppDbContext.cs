using EcDataguard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using EcDataguard.Application.Abstractions;

namespace EcDataguard.Infrastructure.EfCore;

public class AppDbContext : DbContext, IAppDbContext
{
    private Guid? _tenantScope;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<ConsoleUser> ConsoleUsers => Set<ConsoleUser>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<EndpointUser> EndpointUsers => Set<EndpointUser>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceDbArtifact> DeviceDbArtifacts => Set<DeviceDbArtifact>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<AgentCommand> AgentCommands => Set<AgentCommand>();
    public DbSet<Classification> Classifications => Set<Classification>();
    public DbSet<Destination> Destinations => Set<Destination>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<EventRecord> Events => Set<EventRecord>();
    public DbSet<Insight> Insights => Set<Insight>();
    public DbSet<AdminAction> AdminActions => Set<AdminAction>();
    public DbSet<SiemDeliveryLog> SiemDeliveryLogs => Set<SiemDeliveryLog>();

    public Guid? CurrentTenantScope => _tenantScope;

    public void SetTenantScope(Guid tenantId) => _tenantScope = tenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.Codigo).IsUnique();
            e.Property(t => t.Plan).HasConversion<string>();
        });

        modelBuilder.Entity<ConsoleUser>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
            e.Property(u => u.Role).HasConversion<string>();
        });

        modelBuilder.Entity<Team>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => new { t.TenantId, t.Name });
        });

        modelBuilder.Entity<EndpointUser>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => new { u.TenantId, u.UserName }).IsUnique();
        });

        modelBuilder.Entity<Device>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => new { d.TenantId, d.Hostname });
            e.Ignore(d => d.Online);
        });

        modelBuilder.Entity<DeviceDbArtifact>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => new { a.TenantId, a.DeviceId });
            e.Property(a => a.Engine).HasConversion<string>();
        });

        modelBuilder.Entity<DeviceToken>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => new { t.TenantId, t.DeviceId });
        });

        modelBuilder.Entity<AgentCommand>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => new { c.DeviceId, c.State });
            e.Property(c => c.Kind).HasConversion<string>();
            e.Property(c => c.State).HasConversion<string>();
        });

        modelBuilder.Entity<Classification>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => new { c.TenantId, c.Name });
            e.HasMany(c => c.Rules).WithOne().HasForeignKey(r => r.ClassificationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClassificationRule>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Type).HasConversion<string>();
        });

        modelBuilder.Entity<Destination>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => new { d.TenantId, d.Name });
            e.Property(d => d.Tier).HasConversion<string>();
        });

        modelBuilder.Entity<Policy>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => new { p.TenantId, p.Enabled });
            e.Property(p => p.Kind).HasConversion<string>();
            e.Property(p => p.Action).HasConversion<string>();
        });

        modelBuilder.Entity<EventRecord>(e =>
        {
            e.HasKey(ev => ev.Id);
            e.HasIndex(ev => ev.IngestedUtc);
            e.HasIndex(ev => new { ev.TenantId, ev.Kind });
            e.HasIndex(ev => new { ev.TenantId, ev.ExternalId }).IsUnique();
            e.Property(ev => ev.Kind).HasConversion<string>();
            e.Property(ev => ev.AppliedAction).HasConversion<string>();
        });

        modelBuilder.Entity<Insight>(e =>
        {
            e.HasKey(i => i.Id);
            e.HasIndex(i => new { i.TenantId, i.Status, i.Severity });
            e.Property(i => i.Severity).HasConversion<string>();
            e.Property(i => i.Status).HasConversion<string>();
        });

        modelBuilder.Entity<AdminAction>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.OccurredUtc);
        });

        modelBuilder.Entity<SiemDeliveryLog>(e =>
        {
            e.HasKey(l => l.Id);
            e.HasIndex(l => l.SentUtc);
        });
    }
}