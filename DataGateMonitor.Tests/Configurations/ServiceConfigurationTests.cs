using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DataGateMonitor.Configurations;
using DataGateMonitor.Services.Api.Interfaces;
using DataGateMonitor.Services.BackgroundServices.Interfaces;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.Services.Helpers.Interfaces;
using DataGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;
using DataGateMonitor.Services.QuotaPlans;
using DataGateMonitor.Services.Tags;
using DataGateMonitor.Services.UserRoles;
using DataGateMonitor.Services.Users;
using DataGateMonitor.Services.Users.Interfaces;
using DataGateMonitor.Services.DataGateOpenVpnManager.OpenVpnProxy;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.Services.StatusStreamLogs;
using DataGateMonitor.Services.XrayNode;
using Xunit;

namespace DataGateMonitor.Tests.Configurations;

public class ServiceConfigurationTests
{
    private static IConfiguration CreateMinimalConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

    [Fact]
    public void ConfigureServices_Registers_KeyApplicationServices()
    {
        var services = new ServiceCollection();
        var config = CreateMinimalConfig();

        var databaseRuntime = DatabaseRuntimeOptions.FromConfiguration(config);
        services.ConfigureServices(config, databaseRuntime);

        AssertRegistered(services, typeof(IOpenVpnClientService));
        AssertRegistered(services, typeof(IVpnServerService));
        AssertRegistered(services, typeof(IXrayNodeApiClient));
        AssertRegistered(services, typeof(IXrayVpnClientSyncService));
        AssertRegistered(services, typeof(IXrayVpnServerStatusLogService));
        AssertRegistered(services, typeof(IVpnDataService));
        AssertRegistered(services, typeof(IVpnServerStatisticsService));
        AssertRegistered(services, typeof(IOpenVpnBackgroundService));
        AssertRegistered(services, typeof(IVpnServerOvpnFileConfigService));
        AssertRegistered(services, typeof(IExternalIpAddressService));
        AssertRegistered(services, typeof(IVpnNodePublicIpLookup));
        AssertRegistered(services, typeof(IVpnServerClientPresenceService));
        AssertRegistered(services, typeof(IUserService));
        AssertRegistered(services, typeof(IQuotaPlanService));
        AssertRegistered(services, typeof(IUserRoleManagementService));
        AssertRegistered(services, typeof(ITagService));
        AssertRegistered(services, typeof(IMicroserviceInfoService));
        AssertRegistered(services, typeof(IVpnServerConflogService));
        AssertRegistered(services, typeof(IOpenVpnMicroserviceClientFactory));
        AssertRegistered(services, typeof(IOpenVpnProxyTrafficFlowSupportChecker));
        AssertRegistered(services, typeof(IOpenVpnProxyTrafficFlowClientFactory));
        AssertRegistered(services, typeof(IStatusCacheGenerationService));
        AssertRegistered(services, typeof(IRedisDatabaseProvider));
        AssertRegistered(services, typeof(IConnectedClientsCounterStore));
        AssertRegistered(services, typeof(IStatusStreamLogStore));
    }

    private static void AssertRegistered(IServiceCollection services, Type serviceType)
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == serviceType);
        Assert.True(descriptor != null, $"Expected service {serviceType.Name} to be registered.");
    }
}
