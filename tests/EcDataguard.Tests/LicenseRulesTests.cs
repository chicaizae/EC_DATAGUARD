using EcDataguard.Application.Services;
using EcDataguard.Domain.Entities;
using Xunit;

namespace EcDataguard.Tests;

public class LicenseRulesTests
{
    [Fact]
    public void UserLimit_DependeDelPlan()
    {
        Assert.Equal(25, LicenseRules.UserLimit(TenantPlan.Standard));
        Assert.Equal(250, LicenseRules.UserLimit(TenantPlan.Premium));
        Assert.Equal(2000, LicenseRules.UserLimit(TenantPlan.Enterprise));
    }

    [Fact]
    public void IsOverLimit_SoloCuandoActivosSuperanLimite()
    {
        Assert.False(LicenseRules.IsOverLimit(25, 25));
        Assert.True(LicenseRules.IsOverLimit(26, 25));
    }

    [Fact]
    public void UsagePercent_CalculaPorcentajeRedondeado()
    {
        Assert.Equal(50m, LicenseRules.UsagePercent(5, 10));
        Assert.Equal(33.33m, LicenseRules.UsagePercent(1, 3));
    }
}
