using DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;
using Xunit;

namespace DataGateMonitor.Tests.DataBase.Services.Query.UserVpnServerAccessRuleTable;

public class UserVpnServerAccessOverridesTests
{
    [Fact]
    public void Allows_WhenEmpty_FollowsQuotaPlanOnly()
    {
        var overrides = UserVpnServerAccessOverrides.None;

        Assert.True(overrides.IsEmpty);
        Assert.True(overrides.Allows(1, allowedByQuotaPlan: true));
        Assert.False(overrides.Allows(1, allowedByQuotaPlan: false));
        Assert.Equal(string.Empty, overrides.CacheScopeSuffix);
    }

    [Fact]
    public void Allows_WhenPersonalGrant_OpensServerOutsidePlan()
    {
        var overrides = new UserVpnServerAccessOverrides([5], []);

        Assert.True(overrides.Allows(5, allowedByQuotaPlan: false));
        Assert.False(overrides.Allows(6, allowedByQuotaPlan: false));
    }

    [Fact]
    public void Allows_WhenPersonalBlock_WinsOverPlanAndGrant()
    {
        var overrides = new UserVpnServerAccessOverrides([5], [5]);

        Assert.False(overrides.Allows(5, allowedByQuotaPlan: true));
        Assert.False(overrides.Allows(5, allowedByQuotaPlan: false));
    }

    [Fact]
    public void Allows_WhenPersonalBlock_WithoutPlan_Denies()
    {
        var overrides = new UserVpnServerAccessOverrides([], [9]);

        Assert.False(overrides.Allows(9, allowedByQuotaPlan: true));
        Assert.True(overrides.Allows(8, allowedByQuotaPlan: true));
    }

    [Fact]
    public void CacheScopeSuffix_OrdersIdsAndFormatsAllowDeny()
    {
        var overrides = new UserVpnServerAccessOverrides([3, 1], [20, 10]);

        Assert.Equal(":allow=1,3:deny=10,20", overrides.CacheScopeSuffix);
    }
}
