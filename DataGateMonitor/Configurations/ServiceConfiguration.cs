using DataGateMonitor.DataBase.Services.Query.UserCredentialTable;
using DataGateMonitor.Hubs.BackgroundService;
using DataGateMonitor.Services.Api;
using DataGateMonitor.Services.Api.Interfaces;
using DataGateMonitor.Services.BackgroundServices;
using DataGateMonitor.Services.BackgroundServices.Interfaces;
using DataGateMonitor.Services.DataGateOpenVpnManager;
using DataGateMonitor.Services.DataGateOpenVpnManager.Events;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.Services.DataGateOpenVpnManager.OpenVpnProxy;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.Services.Helpers.Interfaces;
using DataGateMonitor.Services.OpenVpnManagementInterfaces;
using DataGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;
using DataGateMonitor.Services.Others;
using DataGateMonitor.Services.QuotaPlans;
using DataGateMonitor.Services.Tags;
using DataGateMonitor.Services.VpnServerGroups;
using DataGateMonitor.Services.UserRoles;
using DataGateMonitor.Services.Users;
using DataGateMonitor.Services.Users.Interfaces;
using DataGateMonitor.Services.DataGateXRayManager.ClientLinks;
using DataGateMonitor.Services.DataGateXRayManager.Events;
using DataGateMonitor.Services.VpnAccess;
using DataGateMonitor.Services.Api.MobileCrashIngest;
using DataGateMonitor.Services.Api.WindowsCrashIngest;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.Services.StatusStreamLogs;
using DataGateMonitor.Services.Performance;
using DataGateMonitor.Data.Interceptors;
using DataGateMonitor.Services.XrayNode;
using System.Net;
using System.Net.Http;
using System.Reflection;
using DataGateMonitor.Serialization;

namespace DataGateMonitor.Configurations;

public static class ServiceConfiguration
{
    public static void ConfigureServices(this IServiceCollection services, IConfiguration configuration,
        DatabaseRuntimeOptions databaseRuntime)
    {
        services.AddSingleton<ApplicationRuntimeInfo>();
        services.AddSingleton<IApplicationStartupHistory>(sp =>
        {
            var env = sp.GetRequiredService<IWebHostEnvironment>();
            var runtimeInfo = sp.GetRequiredService<ApplicationRuntimeInfo>();
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown version";
            return new ApplicationStartupHistory(env, runtimeInfo, version, env.EnvironmentName);
        });

        // var frontendSettings = configuration.GetSection("Frontend").Get<FrontendSettings>();
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAllOriginsWithCredentials", policy =>
            {
                policy
                    .SetIsOriginAllowed(_ => true)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
        services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
            })
            .AddNewtonsoftJsonProtocol(options => options.PayloadSerializerSettings = ProjectJson.WebSettings);
        services.ConfigureHttpClientDefaults(builder =>
        {
            builder.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                // Keep TCP/TLS connections hot and reuse them between polling cycles.
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                // Periodically rotate pooled connections to honor DNS updates.
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                MaxConnectionsPerServer = 64,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            });
            builder.SetHandlerLifetime(TimeSpan.FromMinutes(10));
        });
        services.AddMemoryCache();
        services.AddSingleton<IApiMemoryCacheService, ApiMemoryCacheService>();
        services.AddSingleton<IStatusCacheGenerationService, StatusCacheGenerationService>();
        services.AddSingleton<IRedisMultiplexerConnector, StackExchangeRedisMultiplexerConnector>();
        services.AddSingleton<IRedisDatabaseProvider, ConfigurationRedisDatabaseProvider>();
        services.AddSingleton<IConnectedClientsCounterStore, RedisConnectedClientsCounterStore>();
        services.AddSingleton<IStatusStreamLogStore, StatusStreamLogStore>();
        services.Configure<PerformanceMonitoringOptions>(configuration.GetSection(PerformanceMonitoringOptions.SectionName));
        services.AddSingleton<IPerformanceSampleStore, PerformanceSampleStore>();
        services.AddSingleton<SlowDbCommandInterceptor>();
        
        services.AddScoped<IOpenVpnClientService, OpenVpnClientService>();
        services.AddScoped<IOpenVpnStateService, OpenVpnStateService>();
        services.AddScoped<IOpenVpnSummaryStatService, OpenVpnSummaryStatService>();
        services.AddScoped<IOpenVpnVersionService, OpenVpnVersionService>();
        
        services.AddScoped<IVpnServerService, VpnServerService>();

        services.AddScoped<IXrayNodeApiClient, XrayNodeApiClient>();
        services.AddScoped<IXrayVpnClientSyncService, XrayVpnClientSyncService>();
        services.AddScoped<IXrayVpnServerStatusLogService, XrayVpnServerStatusLogService>();
        
        services.AddScoped<IVpnDataService, VpnDataService>();
        services.AddSingleton<IVpnServerPostSetupService, VpnServerPostSetupService>();
        services.AddScoped<IVpnServerStatisticsService, VpnServerStatisticsService>();

        services.AddSingleton<VpnServerStatusManager>();
        services.AddSingleton<VpnServerProcessorFactory>();

        services.AddHttpClient(XrayNodeApiClient.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddSingleton<OpenVpnBackgroundService>();
        services.AddSingleton<IOpenVpnBackgroundService>(provider => provider.GetRequiredService<OpenVpnBackgroundService>());
        if (databaseRuntime.IsConnectionConfigured)
        {
            services.AddScoped<ITrafficDailyRollupRunner, TrafficDailyRollupRunner>();
            services.AddHostedService(provider => provider.GetRequiredService<OpenVpnBackgroundService>());
            services.AddHostedService<OpenVpnEventBackgroundService>();
            services.AddHostedService<XrayDnsEventBackgroundService>();
            services.AddHostedService<OpenVpnStatusStreamPublisher>();
            services.AddHostedService<OpenVpnProxyTrafficFlowBackgroundService>();
            services.AddHostedService<TrafficDailyRollupBackgroundService>();
            services.AddHostedService<FreeTierOpenVpnSessionEnforcementBackgroundService>();
            services.AddHostedService<FreeTierUnsubscribedVpnUsersDailyDigestBackgroundService>();
        }

        services.AddScoped<IVpnEventLogService, VpnEventLogService>();
        services.AddScoped<IVpnDnsQueryLogService, VpnDnsQueryLogService>();
        services.AddSingleton<IOpenVpnEventClientFactory, OpenVpnEventClientFactory>();
        services.AddSingleton<IXrayDnsEventClientFactory, XrayDnsEventClientFactory>();

        services.AddScoped<IVpnServerOvpnFileConfigService, VpnServerOvpnFileConfigService>();
        services.AddScoped<IVpnServerPiHoleConfigService, VpnServerPiHoleConfigService>();
        services.AddScoped<IVpnServerOpenVpnProcessService, VpnServerOpenVpnProcessService>();
        services.AddScoped<ISettingsService, SettingsService>();
        
        services.AddHttpClient<IExternalIpAddressService, ExternalIpAddressService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<IVpnNodePublicIpLookup, VpnNodePublicIpLookup>();
        services.AddScoped<IVpnServerClientPresenceService, VpnServerClientPresenceService>();

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserPasswordHistoryService, UserPasswordHistoryService>();
        services.AddScoped<IUserMergeService, UserMergeService>();
        services.AddScoped<ITelegramAccountLinkService, TelegramAccountLinkService>();
        // MS.DI does not auto-wrap Lazy<T>; needed to break TelegramAccountLink ↔ free-tier reminder cycle.
        services.AddScoped(sp => new Lazy<ITelegramAccountLinkService>(
            () => sp.GetRequiredService<ITelegramAccountLinkService>()));
        services.AddScoped<IFreeTierAccessComplianceService, FreeTierAccessComplianceService>();
        services.AddScoped<IFreeTierOpenVpnSessionEnforcementService, FreeTierOpenVpnSessionEnforcementService>();
        services.AddScoped<IOpenVpnDisconnectExecutor, OpenVpnDisconnectExecutor>();
        services.AddScoped<IFreeTierEnforcementOverviewService, FreeTierEnforcementOverviewService>();
        services.AddScoped<IFreeTierGraceDisconnectNotifier, FreeTierGraceDisconnectNotifier>();
        services.AddScoped<IFreeTierUnsubscribedUserReminderService, FreeTierUnsubscribedUserReminderService>();
        services.AddScoped<IFreeTierUnsubscribedVpnUsersDailyDigestService, FreeTierUnsubscribedVpnUsersDailyDigestService>();
        
        services.AddScoped<IQuotaPlanService, QuotaPlanService>();
        services.AddScoped<IUserRoleManagementService, UserRoleManagementService>();
        services.AddScoped<IQuotaPlanAllowedServerService, QuotaPlanAllowedServerService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<IVpnServerGroupService, VpnServerGroupService>();
        
        services.AddScoped<IUserCredentialQueryService, UserCredentialQueryService>();
        services.AddScoped<ICrashReportParser, CrashReportParser>();
        services.AddScoped<IMobileCrashIngestService, MobileCrashIngestService>();
        services.AddScoped<IWindowsCrashIngestService, WindowsCrashIngestService>();
        services.AddSingleton<ICrashIngestRateLimiter, MemoryCrashIngestRateLimiter>();
        services.AddSingleton<ICrashIngestMetrics, CrashIngestMetrics>();
        
        #region DataGateOpenVpnManager

        services.AddHttpClient();
        services.AddScoped<ICertApiClient, CertApiClient>();
        services.AddScoped<IOvpnFileApiClient, OvpnFileApiClient>();
        services.AddScoped<IVpnServerQuotaPlanAccessGuard, VpnServerQuotaPlanAccessGuard>();
        services.AddScoped<IOvpnFileApiService, OvpnFileApiService>();
        services.AddScoped<IXrayClientLinkMicroserviceClient, XrayClientLinkMicroserviceClient>();
        services.AddScoped<IXrayClientLinkService, XrayClientLinkService>();
        services.AddScoped<IMicroserviceInfoService, MicroserviceInfoService>();
        services.AddScoped<IProxyClientLookupService, ProxyClientLookupService>();
        services.AddScoped<IVpnServerConflogService, VpnServerConflogService>();

        services.AddSingleton<IOpenVpnMicroserviceClientFactory, OpenVpnMicroserviceClientFactory>();
        services.AddSingleton<IOpenVpnProxyTrafficFlowSupportChecker, OpenVpnProxyTrafficFlowSupportChecker>();
        services.AddSingleton<IOpenVpnProxyTrafficFlowClientFactory, OpenVpnProxyTrafficFlowClientFactory>();

        #endregion
    }
}