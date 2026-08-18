using DataGateMonitor.Services.Api;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Tests.Services.Api;

public class LegacyOpenVpnV1ServerFilterTests
{
    [Fact]
    public void OpenVpnTcpConfig_IsVisible_EvenWhenNameOrTagSayUdp()
    {
        Assert.True(LegacyOpenVpnV1ServerFilter.IsVisibleToLegacyTcpOpenVpnClient(
            VpnServerType.OpenVpn,
            "client\nproto tcp\nremote {{server_ip}} 1194"));
    }

    [Fact]
    public void Xray_IsHidden_RegardlessOfConfig()
    {
        Assert.False(LegacyOpenVpnV1ServerFilter.IsVisibleToLegacyTcpOpenVpnClient(
            VpnServerType.Xray,
            "client\nproto tcp\nremote {{server_ip}} 1194"));
    }

    [Theory]
    [InlineData("client\nproto udp\nremote {{server_ip}} 1194")]
    [InlineData("client\nproto UDP\nremote {{server_ip}} 1194")]
    [InlineData("client\nproto udp4\nremote {{server_ip}} 1194")]
    [InlineData("client\nproto udp6\nremote {{server_ip}} 1194")]
    [InlineData("  proto udp")]
    public void OpenVpn_UdpProtoInConfig_IsHidden(string template)
    {
        Assert.False(LegacyOpenVpnV1ServerFilter.IsVisibleToLegacyTcpOpenVpnClient(
            VpnServerType.OpenVpn,
            template));
    }

    [Fact]
    public void OpenVpn_UdpOnlyInCommentOrElsewhere_StaysVisible()
    {
        Assert.True(LegacyOpenVpnV1ServerFilter.IsVisibleToLegacyTcpOpenVpnClient(
            VpnServerType.OpenVpn,
            "# proto udp\nclient\nproto tcp\nremote {{server_ip}} 1194"));
    }

    [Fact]
    public void OpenVpn_MissingConfig_StaysVisible()
    {
        Assert.True(LegacyOpenVpnV1ServerFilter.IsVisibleToLegacyTcpOpenVpnClient(
            VpnServerType.OpenVpn,
            null));
    }
}
