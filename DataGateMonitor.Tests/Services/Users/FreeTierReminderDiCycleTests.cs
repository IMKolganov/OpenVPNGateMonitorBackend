using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;
using DataGateMonitor.DataBase.Services.Query.UserQuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.DataBase.UnitOfWork;
using DataGateMonitor.Models;
using DataGateMonitor.Models.Helpers;
using DataGateMonitor.Services.AdminEmail;
using DataGateMonitor.Services.Api.Auth.EmailConfirmation;
using DataGateMonitor.Services.EmailTemplates;
using DataGateMonitor.Services.Others;
using DataGateMonitor.Services.Others.Notifications;
using DataGateMonitor.Services.TelegramBot.Interfaces;
using DataGateMonitor.Services.Users;
using DataGateMonitor.Services.Users.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DataGateMonitor.Tests.Services.Users;

/// <summary>
/// Reproduces production Google-login 500s from the TelegramAccountLink ↔ free-tier reminder DI cycle.
/// </summary>
public class FreeTierReminderDiCycleTests
{
    [Fact]
    public void Without_Lazy_registration_ValidateOnBuild_fails()
    {
        var services = BuildCycleServices(registerLazy: false);

        var ex = Assert.ThrowsAny<Exception>(() =>
            services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true }));

        Assert.Contains("Lazy", ex.ToString(), StringComparison.Ordinal);
        Assert.Contains("ITelegramAccountLinkService", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void With_Lazy_registration_TelegramAccountLink_and_Reminder_resolve()
    {
        var services = BuildCycleServices(registerLazy: true);
        using var root = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        using var scope = root.CreateScope();
        var sp = scope.ServiceProvider;

        // Same graph Google login hits via merge/compliance paths.
        var link = sp.GetRequiredService<ITelegramAccountLinkService>();
        var reminder = sp.GetRequiredService<IFreeTierUnsubscribedUserReminderService>();
        var lazy = sp.GetRequiredService<Lazy<ITelegramAccountLinkService>>();

        Assert.NotNull(link);
        Assert.NotNull(reminder);
        Assert.Same(link, lazy.Value);
    }

    private static ServiceCollection BuildCycleServices(bool registerLazy)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
        services.AddSingleton(Options.Create(new TelegramChannelSettings
        {
            RequiredChannelUsername = "datagateapp",
            BotUsername = "DataGateVPNBot",
        }));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddSingleton(Mock.Of<IUserQueryService>());
        services.AddSingleton(Mock.Of<IUserIdentityLinkQueryService>());
        services.AddSingleton(Mock.Of<ITelegramUserService>());
        services.AddSingleton(Mock.Of<ITelegramAdminAlertService>());
        services.AddSingleton(Mock.Of<IUnitOfWork>());
        services.AddSingleton(Mock.Of<ICommandService<User, int>>());
        services.AddSingleton(Mock.Of<ICommandService<MergedUserArchive, int>>());
        services.AddSingleton(Mock.Of<IUserQuotaPlanQueryService>());
        services.AddSingleton(Mock.Of<IQuotaPlanQueryService>());
        services.AddSingleton(Mock.Of<ITelegramChannelMembershipChecker>());
        services.AddSingleton(Mock.Of<IAppNotificationFacade>());
        services.AddSingleton(Mock.Of<ISettingsService>());
        services.AddSingleton(Mock.Of<ITelegramDirectMessageSender>());
        services.AddSingleton(Mock.Of<ILocalizationService>());
        services.AddSingleton(Mock.Of<IEmailSenderService>());
        services.AddSingleton(Mock.Of<ISystemTransactionalEmailService>());
        services.AddSingleton(Mock.Of<ISentEmailLogService>());
        services.AddSingleton(Mock.Of<ILogger<TelegramAccountLinkService>>());
        services.AddSingleton(Mock.Of<ILogger<UserMergeService>>());
        services.AddSingleton(Mock.Of<ILogger<FreeTierAccessComplianceService>>());
        services.AddSingleton(Mock.Of<ILogger<FreeTierUnsubscribedUserReminderService>>());

        services.AddScoped<IUserMergeService, UserMergeService>();
        services.AddScoped<ITelegramAccountLinkService, TelegramAccountLinkService>();
        if (registerLazy)
        {
            // Mirrors ServiceConfiguration — MS.DI does not auto-provide Lazy<T>.
            services.AddScoped(sp => new Lazy<ITelegramAccountLinkService>(
                () => sp.GetRequiredService<ITelegramAccountLinkService>()));
        }

        services.AddScoped<IFreeTierAccessComplianceService, FreeTierAccessComplianceService>();
        services.AddScoped<IFreeTierUnsubscribedUserReminderService, FreeTierUnsubscribedUserReminderService>();

        return services;
    }
}
