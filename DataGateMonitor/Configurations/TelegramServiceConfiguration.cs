using DataGateMonitor.Services.TelegramBot;
using DataGateMonitor.Services.TelegramBot.Interfaces;
using DataGateMonitor.Models.Helpers;

namespace DataGateMonitor.Configurations;

public static class TelegramServiceConfiguration
{
    public static void ConfigureTelegramServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TelegramChannelSettings>(configuration.GetSection(TelegramChannelSettings.SectionName));
        services.PostConfigure<TelegramChannelSettings>(TelegramChannelSettings.ApplyEnvOverrides);
        services.AddScoped<ITelegramChannelMembershipChecker, TelegramChannelMembershipChecker>();
        services.AddScoped<ITelegramDirectMessageSender, TelegramDirectMessageSender>();
        services.AddScoped<ITelegramAdminAlertService, TelegramAdminAlertService>();

        services.AddScoped<ITelegramBotUserProfilePhotoService, TelegramBotUserProfilePhotoService>();
        services.AddScoped<ITelegramUserService, TelegramUserService>();
        services.AddScoped<ILocalizationService, LocalizationService>();
        services.AddScoped<IIncomingMessageLogService, IncomingMessageLogService>();
    }
}