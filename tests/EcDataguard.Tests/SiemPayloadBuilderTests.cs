using System.Text.Json;
using EcDataguard.Application.Services;
using EcDataguard.Domain.Entities;
using EcDataguard.Domain.Enums;
using Xunit;

namespace EcDataguard.Tests;

public class SiemPayloadBuilderTests
{
    [Fact]
    public void AdminAction_IncluyeCamposOcsfBasicos()
    {
        var action = new AdminAction
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ActorName = "admin@ecodataguard.local",
            Section = "Devices",
            Activity = "Revocado token",
            ContentJson = "{}",
            OccurredUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        using var json = JsonDocument.Parse(SiemPayloadBuilder.AdminAction(action));
        var root = json.RootElement;

        Assert.Equal("Admin Activity", root.GetProperty("activity_name").GetString());
        Assert.Equal("Application Activity", root.GetProperty("class_name").GetString());
        Assert.Equal(action.TenantId.ToString(), root.GetProperty("tenant_uid").GetGuid().ToString());
        Assert.Equal("Devices", root.GetProperty("api").GetProperty("service").GetProperty("name").GetString());
    }

    [Fact]
    public void Insight_IncluyeFindingYSeveridad()
    {
        var insight = new Insight
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Severity = InsightSeverity.High,
            Status = InsightStatus.Open,
            Reason = "Blocked by policy",
            RelatedEventCount = 1,
            CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LastActivityUtc = new DateTime(2026, 1, 1, 0, 5, 0, DateTimeKind.Utc)
        };

        using var json = JsonDocument.Parse(SiemPayloadBuilder.Insight(insight));
        var root = json.RootElement;

        Assert.Equal("Security Finding", root.GetProperty("activity_name").GetString());
        Assert.Equal("High", root.GetProperty("severity").GetString());
        Assert.Equal("Blocked by policy", root.GetProperty("finding").GetProperty("title").GetString());
        Assert.Equal(1, root.GetProperty("finding").GetProperty("related_events").GetInt32());
    }
}
