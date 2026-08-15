using DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.Models;
using DataGateMonitor.Models.Helpers;
using DataGateMonitor.Services.AdminEmail;
using DataGateMonitor.Services.Api.Auth.EmailConfirmation;
using DataGateMonitor.Services.EmailTemplates;
using DataGateMonitor.Services.Others;
using DataGateMonitor.Services.TelegramBot.Interfaces;
using DataGateMonitor.Services.Users;
using DataGateMonitor.Services.Users.Interfaces;
using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Responses;
using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Enums;
using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Responses;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DataGateMonitor.Tests.Services.Users;

public class FreeTierUnsubscribedNotificationsTests
{
    private static void SetupBoolSetting(Mock<ISettingsService> settings, string key, bool value)
    {
        settings.Setup(s => s.GetValueAsync<string>($"{key}_Type", It.IsAny<CancellationToken>()))
            .ReturnsAsync("bool");
        settings.Setup(s => s.GetValueAsync<bool>(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value);
    }

    private static FreeTierUnsubscribedUserReminderService CreateReminderSut(
        Mock<ISettingsService> settings,
        Mock<ITelegramDirectMessageSender> sender,
        IMemoryCache? cache = null,
        Mock<ILocalizationService>? localization = null,
        Mock<IUserIdentityLinkQueryService>? links = null,
        Mock<IUserQueryService>? users = null,
        Mock<IEmailSenderService>? email = null,
        Mock<ISystemTransactionalEmailService>? templates = null,
        Mock<ISentEmailLogService>? sentLog = null,
        Mock<ITelegramAccountLinkService>? accountLink = null)
    {
        var loc = localization ?? new Mock<ILocalizationService>();
        loc.Setup(l => l.GetTextForTelegramUser(
                FreeTierUnsubscribedUserReminderService.LocalizationKey,
                It.IsAny<long>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<DataGateMonitor.SharedModels.Enums.Language?>()))
            .ReturnsAsync("📢 Please subscribe to {channel}\n{channelUrl}");

        return new FreeTierUnsubscribedUserReminderService(
            settings.Object,
            sender.Object,
            loc.Object,
            (links ?? new Mock<IUserIdentityLinkQueryService>()).Object,
            (users ?? new Mock<IUserQueryService>()).Object,
            (email ?? new Mock<IEmailSenderService>()).Object,
            (templates ?? new Mock<ISystemTransactionalEmailService>()).Object,
            (sentLog ?? new Mock<ISentEmailLogService>()).Object,
            (accountLink ?? new Mock<ITelegramAccountLinkService>()).Object,
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new TelegramChannelSettings
            {
                RequiredChannelUsername = "datagateapp",
                BotUsername = "DataGateVPNBot",
            }),
            Mock.Of<ILogger<FreeTierUnsubscribedUserReminderService>>());
    }

    [Fact]
    public async Task Reminder_WhenDisabled_DoesNotSend()
    {
        var settings = new Mock<ISettingsService>();
        SetupBoolSetting(settings, FreeTierAccessSettingsKeys.SendUnsubscribedUserReminders, false);
        var sender = new Mock<ITelegramDirectMessageSender>(MockBehavior.Strict);
        var sut = CreateReminderSut(settings, sender);

        await sut.TryRemindAsync(1, "test", CancellationToken.None);
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Reminder_WhenEnabled_SendsLocalizedTextOncePerCooldown()
    {
        var settings = new Mock<ISettingsService>();
        SetupBoolSetting(settings, FreeTierAccessSettingsKeys.SendUnsubscribedUserReminders, true);
        var sender = new Mock<ITelegramDirectMessageSender>();
        sender.Setup(s => s.TrySendMessageAsync(42, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = CreateReminderSut(settings, sender);
        await sut.TryRemindAsync(42, "first", CancellationToken.None);
        await sut.TryRemindAsync(42, "second", CancellationToken.None);

        sender.Verify(
            s => s.TrySendMessageAsync(
                42,
                It.Is<string>(t => t.Contains("@datagateapp") && t.Contains("https://t.me/datagateapp")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ForceRemind_Telegram_ResolvesUserIdAndSendsEvenWhenDisabled()
    {
        var settings = new Mock<ISettingsService>(MockBehavior.Strict);
        var sender = new Mock<ITelegramDirectMessageSender>();
        sender.Setup(s => s.TrySendMessageAsync(439938925, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var links = new Mock<IUserIdentityLinkQueryService>();
        links.Setup(l => l.GetListByUserId(22, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new UserIdentityLink { Provider = "telegram", ExternalId = "439938925", UserId = 22 },
                new UserIdentityLink { Provider = "google", ExternalId = "g", UserId = 22 },
            ]);

        var sut = CreateReminderSut(settings, sender, links: links);
        var result = await sut.ForceRemindAsync("22", FreeTierChannelSubscribeRemindChannel.Telegram, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(FreeTierChannelSubscribeRemindChannel.Telegram, result.Channel);
        Assert.Contains("439938925", result.Message);
        settings.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ForceRemind_Email_SendsAndLogs()
    {
        var settings = new Mock<ISettingsService>(MockBehavior.Strict);
        var users = new Mock<IUserQueryService>();
        users.Setup(u => u.GetById(150, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 150, DisplayName = "Tatyana", Email = "t@example.com" });

        var accountLink = new Mock<ITelegramAccountLinkService>();
        accountLink.Setup(a => a.RequestLinkCodeAsync(150, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RequestTelegramAccountLinkCodeResponse
            {
                Code = "ABCD2345",
                ExpiresInSeconds = 900,
            });

        var templates = new Mock<ISystemTransactionalEmailService>();
        templates.Setup(t => t.GetFreeTierChannelSubscribeReminderAsync(
                "Tatyana",
                "@datagateapp",
                "https://t.me/datagateapp",
                "ABCD2345",
                "https://t.me/DataGateVPNBot?start=link_ABCD2345",
                15,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(("subj", "<html>body</html>"));

        var email = new Mock<IEmailSenderService>();
        var sentLog = new Mock<ISentEmailLogService>();

        var sut = CreateReminderSut(
            settings,
            new Mock<ITelegramDirectMessageSender>(MockBehavior.Strict),
            users: users,
            email: email,
            templates: templates,
            sentLog: sentLog,
            accountLink: accountLink);

        var result = await sut.ForceRemindAsync("150", FreeTierChannelSubscribeRemindChannel.Email, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("t@example.com", result.Email);
        Assert.Contains("ABCD2345", result.Message);
        email.Verify(e => e.SendAsync("t@example.com", "subj", "<html>body</html>", It.IsAny<CancellationToken>()), Times.Once);
        sentLog.Verify(l => l.LogAsync(150, "t@example.com", "subj", "<html>body</html>", true, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForceRemind_Email_WhenNoEmail_FailsWithoutSend()
    {
        var users = new Mock<IUserQueryService>();
        users.Setup(u => u.GetById(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 7, DisplayName = "NoMail", Email = null });
        var email = new Mock<IEmailSenderService>(MockBehavior.Strict);

        var sut = CreateReminderSut(
            new Mock<ISettingsService>(MockBehavior.Strict),
            new Mock<ITelegramDirectMessageSender>(MockBehavior.Strict),
            users: users,
            email: email);

        var result = await sut.ForceRemindAsync("7", FreeTierChannelSubscribeRemindChannel.Email, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("no email", result.Message, StringComparison.OrdinalIgnoreCase);
        email.VerifyNoOtherCalls();
    }

    [Fact]
    public void Digest_BuildMessage_IncludesProvidersAndEmail()
    {
        var text = FreeTierUnsubscribedVpnUsersDailyDigestService.BuildDigestMessage(
            new DateOnly(2026, 8, 9),
            [
                new FreeTierEnforcementCandidateDto
                {
                    UserId = 150,
                    DisplayName = "Татьяна Еленина",
                    Email = "tatyana@example.com",
                    IdentityProviders = ["google"],
                    VpnServerName = "Helsinki 3",
                    IsMergedAccount = false,
                },
                new FreeTierEnforcementCandidateDto
                {
                    UserId = 7,
                    DisplayName = "Alice",
                    Email = "alice@gmail.com",
                    IdentityProviders = ["google", "telegram"],
                    TelegramId = 111,
                    VpnServerName = "eu1",
                    IsMergedAccount = true,
                },
            ]);

        Assert.Contains("#150 Татьяна Еленина | google | tatyana@example.com | TG:— | Helsinki 3", text);
        Assert.Contains("#7 Alice | google+telegram | alice@gmail.com | TG:111 | eu1", text);
        Assert.Contains("/remind_channel_email", text);
        Assert.Contains("TG #id", text);
        Assert.Contains("/unsubscribed_vpn_users", text);
    }

    [Fact]
    public async Task Digest_LiveFetch_MarksDailySatisfied_SoBackgroundSkips()
    {
        var overview = new Mock<IFreeTierEnforcementOverviewService>();
        overview.Setup(o => o.GetUnsubscribedConnectedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetFreeTierEnforcementCandidatesResponse { Candidates = [] });

        var sender = new Mock<ITelegramDirectMessageSender>(MockBehavior.Strict);
        var telegramUsers = new Mock<ITelegramUserService>();
        telegramUsers.Setup(t => t.GetAdminsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DataGateMonitor.Models.TelegramBotUser { TelegramId = 1 }]);

        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetValueAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("bool");
        settings.Setup(s => s.GetValueAsync<bool>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new FreeTierUnsubscribedVpnUsersDailyDigestService(
            settings.Object,
            overview.Object,
            telegramUsers.Object,
            sender.Object,
            cache,
            Mock.Of<ILogger<FreeTierUnsubscribedVpnUsersDailyDigestService>>());

        _ = await sut.BuildDigestAsync(CancellationToken.None);
        sut.MarkDailyDigestSatisfiedForToday();
        await sut.TrySendDailyDigestAsync(CancellationToken.None);

        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Digest_BuildDigestAsync_ReturnsTextAndCandidates()
    {
        var overview = new Mock<IFreeTierEnforcementOverviewService>();
        overview.Setup(o => o.GetUnsubscribedConnectedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetFreeTierEnforcementCandidatesResponse
            {
                Candidates =
                [
                    new FreeTierEnforcementCandidateDto
                    {
                        UserId = 1,
                        DisplayName = "Bob",
                        Email = "bob@example.com",
                        IdentityProviders = ["google"],
                        IsConnected = true,
                    },
                ],
            });

        var sut = new FreeTierUnsubscribedVpnUsersDailyDigestService(
            Mock.Of<ISettingsService>(),
            overview.Object,
            Mock.Of<ITelegramUserService>(),
            Mock.Of<ITelegramDirectMessageSender>(),
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<ILogger<FreeTierUnsubscribedVpnUsersDailyDigestService>>());

        var digest = await sut.BuildDigestAsync(CancellationToken.None);

        Assert.Contains("Bob", digest.Text);
        Assert.Contains("google", digest.Text);
        Assert.Single(digest.Candidates);
        Assert.Equal("bob@example.com", digest.Candidates[0].Email);
    }

    [Fact]
    public void ApplyPlaceholders_ReplacesChannelAndUrl()
    {
        var text = FreeTierUnsubscribedUserReminderService.ApplyPlaceholders(
            "go to {channel} via {channelUrl}",
            "@datagateapp",
            "https://t.me/datagateapp");

        Assert.Equal("go to @datagateapp via https://t.me/datagateapp", text);
    }
}
