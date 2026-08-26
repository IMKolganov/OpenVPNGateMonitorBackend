using DataGateMonitor.Serialization;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.Info;
using DataGateMonitor.SharedModels.Enums;
using Xunit;

namespace DataGateMonitor.Tests.Services.Helpers;

public class VpnServerNodeIdentityResolverTests
{
    [Fact]
    public void TryResolveManagerApiPort_UsesExplicitApiUrlPort()
    {
        var port = VpnServerNodeIdentityResolver.TryResolveManagerApiPort(
            "https://xs1-nor.datagateapp.com:9443/",
            VpnServerType.Xray,
            conflogPayloadJson: null);

        Assert.Equal(9443, port);
    }

    [Fact]
    public void TryResolveManagerApiPort_FallsBackToConflogForFrontedUrl()
    {
        var payload = ProjectJson.Serialize(new VpnMicroserviceDiagnosticsDto
        {
            ServerType = VpnServerType.OpenVpn,
            OpenVpn = new RootOpenVpnInfoResponse
            {
                Config = new ConfigInfoResponse { ApiPort = "5010" }
            }
        });

        var port = VpnServerNodeIdentityResolver.TryResolveManagerApiPort(
            "https://s1-nor.datagateapp.com/",
            VpnServerType.OpenVpn,
            payload);

        Assert.Equal(5010, port);
    }

    [Fact]
    public void BuildNodeIdentityKey_RequiresIpAndPort()
    {
        Assert.Null(VpnServerNodeIdentityResolver.BuildNodeIdentityKey(null, 5010, VpnServerType.OpenVpn));
        Assert.Null(VpnServerNodeIdentityResolver.BuildNodeIdentityKey("81.27.109.193", null, VpnServerType.OpenVpn));

        var key = VpnServerNodeIdentityResolver.BuildNodeIdentityKey(
            "81.27.109.193",
            5010,
            VpnServerType.OpenVpn);

        Assert.NotNull(key);
        Assert.Equal("81.27.109.193", key.Value.Ip);
        Assert.Equal(5010, key.Value.Port);
    }

    [Fact]
    public void TryResolvePublicIp_PrefersVpnServerIpFromConfig()
    {
        var ip = VpnServerNodeIdentityResolver.TryResolvePublicIp(
            vpnServerIp: "212.147.237.141",
            announcePublicIp: "1.2.3.4",
            apiUrlHostIp: "5.6.7.8");

        Assert.Equal("212.147.237.141", ip);
    }
}
