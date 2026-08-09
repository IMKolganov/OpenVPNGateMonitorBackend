using DataGateMonitor.DataBase.Services.Command;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Command.VpnServerClientTable;
using DataGateMonitor.DataBase.Services.Query;
using DataGateMonitor.DataBase.Services.Query.ClientApplicationTable;
using DataGateMonitor.DataBase.Services.Query.DeviceTable;
using DataGateMonitor.DataBase.Services.Query.IncomingMessageLogTable;
using DataGateMonitor.DataBase.Services.Query.IssuedOvpnFileTable;
using DataGateMonitor.DataBase.Services.Query.IssuedOvpnFileTokenTable;
using DataGateMonitor.DataBase.Services.Query.IssuedXrayClientLinkTable;
using DataGateMonitor.DataBase.Services.Query.IssuedXrayClientLinkTokenTable;
using DataGateMonitor.DataBase.Services.Query.LocalizationTextTable;
using DataGateMonitor.DataBase.Services.Query.NotificationRecipientTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerClientTable;
using DataGateMonitor.DataBase.Services.Query.VpnDnsQueryLogTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerPiHoleConfigTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerEventLogTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerStatusLogTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerConflogTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTagTable;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanAllowedServerTable;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.TagTable;
using DataGateMonitor.DataBase.Services.Query.RoleTable;
using DataGateMonitor.DataBase.Services.Query.TelegramBotUserProfilePhotoTable;
using DataGateMonitor.DataBase.Services.Query.TelegramBotUserTable;
using DataGateMonitor.DataBase.Services.Query.TelegramUserLanguagePreferenceTable;
using DataGateMonitor.DataBase.Services.Query.UserCredentialTable;
using DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;
using DataGateMonitor.DataBase.Services.Query.UserQuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.UserRefreshTokenTable;
using DataGateMonitor.DataBase.Services.Query.UserRoleTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.DataBase.Services.Query.TvLoginSessionTable;

namespace DataGateMonitor.Configurations;

public static class QueryCommandConfiguration
{
    public static void ConfigureQueryCommand(this IServiceCollection services)
    {
        // Generic services
        services.AddScoped(typeof(IQueryService<,>), typeof(EfQueryService<,>));
        services.AddScoped(typeof(ICommandService<,>), typeof(EfCommandService<,>));
        services.AddScoped<ITransactionRunner, EfTransactionRunner>();
        services.AddScoped<IVpnServerClientUpsertService, VpnServerClientUpsertService>();
        
        services.AddScoped<IClientApplicationQueryService, ClientApplicationQueryService>();
        services.AddScoped<IIncomingMessageLogQueryService, IncomingMessageLogQueryService>();
        services.AddScoped<IIssuedOvpnFileQueryService, IssuedOvpnFileQueryService>();
        services.AddScoped<IIssuedOvpnFileTokenQueryService, IssuedOvpnFileTokenQueryService>();
        services.AddScoped<IIssuedXrayClientLinkQueryService, IssuedXrayClientLinkQueryService>();
        services.AddScoped<IIssuedXrayClientLinkTokenQueryService, IssuedXrayClientLinkTokenQueryService>();
        services.AddScoped<ILocalizationTextQueryService, LocalizationTextQueryService>();
        services.AddScoped<INotificationRecipientQueryService, NotificationRecipientQueryService>();

        services.AddScoped<IOpenVpnGeoQueryService, OpenVpnGeoQueryService>();
        services.AddScoped<IOverviewTrafficAggregator, OverviewTrafficAggregator>();
        services.AddScoped<IOverviewTrafficDailyRollupService, OverviewTrafficDailyRollupService>();
        services.AddScoped<IOpenVpnOverviewSeriesQuery, OpenVpnOverviewSeriesQuery>();
        services.AddScoped<IOpenVpnOverviewTotalsQuery, OpenVpnOverviewTotalsQuery>();
        services.AddScoped<IVpnServerClientOverviewQuery, VpnServerClientOverviewQuery>();
        services.AddScoped<IVpnServerClientQueryService, VpnServerClientQueryService>();
        services.AddScoped<IVpnServerEventLogQueryService, VpnServerEventLogQueryService>();
        services.AddScoped<IVpnDnsQueryLogQueryService, VpnDnsQueryLogQueryService>();
        services.AddScoped<IVpnServerPiHoleConfigQueryService, VpnServerPiHoleConfigQueryService>();
        services.AddScoped<IVpnServerOvpnFileConfigQueryService, VpnServerOvpnFileConfigQueryService>();
        services.AddScoped<IVpnServerStatusLogQueryService, VpnServerStatusLogQueryService>();
        services.AddScoped<IVpnServerQueryService, VpnServerQueryService>();
        services.AddScoped<ITelegramBotUserQueryService, TelegramBotUserQueryService>();
        services.AddScoped<ITelegramBotUserProfilePhotoQueryService, TelegramBotUserProfilePhotoQueryService>();
        services.AddScoped<ITelegramUserLanguagePreferenceQueryService, TelegramUserLanguagePreferenceQueryService>();
        
        
        services.AddScoped<IUserCredentialQueryService, UserCredentialQueryService>();
        services.AddScoped<IUserQueryService, UserQueryService>();
        services.AddScoped<IUserIdentityLinkQueryService, UserIdentityLinkQueryService>();
        services.AddScoped<IQuotaPlanQueryService, QuotaPlanQueryService>();
        services.AddScoped<IUserRoleQueryService, UserRoleQueryService>();
        services.AddScoped<IRoleQueryService, RoleQueryService>();
        services.AddScoped<IUserQuotaPlanQueryService, UserQuotaPlanQueryService>();
        services.AddScoped<IQuotaPlanAllowedServerQueryService, QuotaPlanAllowedServerQueryService>();
        services.AddScoped<ITagQueryService, TagQueryService>();
        services.AddScoped<IVpnServerTagQueryService, VpnServerTagQueryService>();
        services.AddScoped<IVpnServerConflogQueryService, VpnServerConflogQueryService>();

        services.AddScoped<IUserRefreshTokenQueryService, UserRefreshTokenQueryService>();
        
        services.AddScoped<IDeviceQueryService, DeviceQueryService>();
        services.AddScoped<ITvLoginSessionQueryService, TvLoginSessionQueryService>();


        // Feature: overview queries
        services.AddScoped<IVpnServerOverviewQuery, VpnServerOverviewQuery>();
        services.AddScoped<IVpnServerQuotaPlanGroupsQuery, VpnServerQuotaPlanGroupsQuery>();

    }
}