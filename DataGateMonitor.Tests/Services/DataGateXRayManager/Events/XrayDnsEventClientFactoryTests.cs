using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.Services.DataGateXRayManager.Events;
using DataGateMonitor.SharedModels.Enums;
using DataGateMonitor.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DataGateMonitor.Tests.Services.DataGateXRayManager.Events;

public class XrayDnsEventClientFactoryTests
{
    private static XrayDnsEventClientFactory CreateFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMicroserviceTokenService>(OpenVpnHubTestHelpers.CreateTokenService());
        services.AddSingleton(Mock.Of<IServiceScopeFactory>());
        return new XrayDnsEventClientFactory(services.BuildServiceProvider());
    }

    private static VpnServer XrayServer(int id = 88, string apiUrl = "https://xs2.datagateapp.com/") =>
        new()
        {
            Id = id,
            ServerName = "Xray test",
            ApiUrl = apiUrl,
            ServerType = VpnServerType.Xray,
        };

    [Fact]
    public void Create_ReturnsCachedClient_ForSameServerId()
    {
        var factory = CreateFactory();
        var server = XrayServer();

        var first = factory.Create(server);
        var second = factory.Create(server);

        Assert.Same(first, second);
        Assert.Equal(server.ApiUrl, first.RegisteredApiUrl);
    }

    [Fact]
    public void Create_RecreatesClient_WhenApiUrlChanges()
    {
        var factory = CreateFactory();
        var first = factory.Create(XrayServer());
        var second = factory.Create(XrayServer(apiUrl: "https://changed-xray.datagateapp.com/"));

        Assert.NotSame(first, second);
        Assert.Equal("https://changed-xray.datagateapp.com/", second.RegisteredApiUrl);
    }

    [Fact]
    public void Create_Throws_WhenServerIsNotXray()
    {
        var factory = CreateFactory();
        var openVpn = OpenVpnHubTestHelpers.OpenVpnServer();

        var ex = Assert.Throws<InvalidOperationException>(() => factory.Create(openVpn));
        Assert.Contains("Xray", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
