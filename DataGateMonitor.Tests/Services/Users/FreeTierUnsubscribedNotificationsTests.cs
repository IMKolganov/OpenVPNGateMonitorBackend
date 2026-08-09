using DataGateMonitor.Models;
using DataGateMonitor.Models.Helpers;
using DataGateMonitor.Services.Others;
using DataGateMonitor.Services.TelegramBot.Interfaces;
using DataGateMonitor.Services.Users;
using DataGateMonitor.Services.Users.Interfaces;
using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Dto;
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

    [Fact]
    public async Task Reminder_WhenDisabled_DoesNotSend()
    {
        var settings = new Mock<ISettingsService>();
        SetupBoolSetting(settings, FreeTierAccessSettingsKeys.SendUnsubscribedUserReminders, false);

        var sender = new Mock<ITelegramDirectMessageSender>(MockBehavior.Strict);
        var sut = new FreeTierUnsubscribedUserReminderService(
            settings.Object,
            sender.Object,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new TelegramChannelSettings { RequiredChannelUsername = "DataGateVPNBot" }),
            Mock.Of<ILogger<FreeTierUnsubscribedUserReminderService>>());

        await sut.TryRemindAsync(1, "test", CancellationToken.None);
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Reminder_WhenEnabled_SendsOncePerCooldown()
    {
        var settings = new Mock<ISettingsService>();
        SetupBoolSetting(settings, FreeTierAccessSettingsKeys.SendUnsubscribedUserReminders, true);

        var sender = new Mock<ITelegramDirectMessageSender>();
        sender.Setup(s => s.TrySendMessageAsync(42, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new FreeTierUnsubscribedUserReminderService(
            settings.Object,
            sender.Object,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new TelegramChannelSettings { RequiredChannelUsername = "DataGateVPNBot" }),
            Mock.Of<ILogger<FreeTierUnsubscribedUserReminderService>>());

        await sut.TryRemindAsync(42, "first", CancellationToken.None);
        await sut.TryRemindAsync(42, "second", CancellationToken.None);

        sender.Verify(
            s => s.TrySendMessageAsync(42, It.Is<string>(t => t.Contains("@DataGateVPNBot")), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Reminder_WhenSettingTypeMissing_DefaultsToEnabledAndSends()
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetValueAsync<string>(
                $"{FreeTierAccessSettingsKeys.SendUnsubscribedUserReminders}_Type",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var sender = new Mock<ITelegramDirectMessageSender>();
        sender.Setup(s => s.TrySendMessageAsync(7, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new FreeTierUnsubscribedUserReminderService(
            settings.Object,
            sender.Object,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new TelegramChannelSettings { RequiredChannelUsername = "DataGateVPNBot" }),
            Mock.Of<ILogger<FreeTierUnsubscribedUserReminderService>>());

        await sut.TryRemindAsync(7, "default-on", CancellationToken.None);

        sender.Verify(
            s => s.TrySendMessageAsync(7, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Reminder_WhenSendFails_DoesNotSetCooldown()
    {
        var settings = new Mock<ISettingsService>();
        SetupBoolSetting(settings, FreeTierAccessSettingsKeys.SendUnsubscribedUserReminders, true);

        var sender = new Mock<ITelegramDirectMessageSender>();
        sender.Setup(s => s.TrySendMessageAsync(9, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = new FreeTierUnsubscribedUserReminderService(
            settings.Object,
            sender.Object,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new TelegramChannelSettings { RequiredChannelUsername = "DataGateVPNBot" }),
            Mock.Of<ILogger<FreeTierUnsubscribedUserReminderService>>());

        await sut.TryRemindAsync(9, "fail-1", CancellationToken.None);
        await sut.TryRemindAsync(9, "fail-2", CancellationToken.None);

        sender.Verify(
            s => s.TrySendMessageAsync(9, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public void Digest_BuildMessage_IncludesOnlineUsers()
    {
        var text = FreeTierUnsubscribedVpnUsersDailyDigestService.BuildDigestMessage(
            new DateOnly(2026, 8, 9),
            1,
            [
                new FreeTierEnforcementCandidateDto
                {
                    UserId = 7,
                    DisplayName = "Alice",
                    TelegramId = 111,
                    VpnServerName = "eu1",
                    IsMergedAccount = true,
                    IsChannelSubscribed = false,
                    IsConnected = true,
                },
            ]);

        Assert.Contains("Currently online: 1", text);
        Assert.Contains("#7 Alice", text);
        Assert.Contains("TG:111", text);
        Assert.Contains("eu1", text);
        Assert.Contains("merged", text);
    }

    [Fact]
    public async Task Digest_WhenDisabled_DoesNotQueryOverviewOrSend()
    {
        var settings = new Mock<ISettingsService>();
        SetupBoolSetting(settings, FreeTierAccessSettingsKeys.DailyUnsubscribedAdminDigest, false);

        var overview = new Mock<IFreeTierEnforcementOverviewService>(MockBehavior.Strict);
        var sender = new Mock<ITelegramDirectMessageSender>(MockBehavior.Strict);
        var telegramUsers = new Mock<ITelegramUserService>(MockBehavior.Strict);

        var sut = new FreeTierUnsubscribedVpnUsersDailyDigestService(
            settings.Object,
            overview.Object,
            telegramUsers.Object,
            sender.Object,
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<ILogger<FreeTierUnsubscribedVpnUsersDailyDigestService>>());

        await sut.TrySendDailyDigestAsync(CancellationToken.None);

        overview.VerifyNoOtherCalls();
        sender.VerifyNoOtherCalls();
        telegramUsers.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Digest_WhenEnabled_SendsToAdmins_AndSkipsSecondCallSameDay()
    {
        var settings = new Mock<ISettingsService>();
        SetupBoolSetting(settings, FreeTierAccessSettingsKeys.DailyUnsubscribedAdminDigest, true);

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
                        IsConnected = true,
                        IsChannelSubscribed = false,
                        TelegramId = 99,
                    },
                ],
                TotalCount = 1,
                ConnectedCount = 1,
            });

        var telegramUsers = new Mock<ITelegramUserService>();
        telegramUsers.Setup(t => t.GetAdminsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TelegramBotUser { TelegramId = 1001, IsAdmin = true }]);

        var sender = new Mock<ITelegramDirectMessageSender>();
        sender.Setup(s => s.TrySendMessageAsync(1001, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new FreeTierUnsubscribedVpnUsersDailyDigestService(
            settings.Object,
            overview.Object,
            telegramUsers.Object,
            sender.Object,
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<ILogger<FreeTierUnsubscribedVpnUsersDailyDigestService>>());

        await sut.TrySendDailyDigestAsync(CancellationToken.None);
        await sut.TrySendDailyDigestAsync(CancellationToken.None);

        overview.Verify(o => o.GetUnsubscribedConnectedAsync(It.IsAny<CancellationToken>()), Times.Once);
        sender.Verify(
            s => s.TrySendMessageAsync(1001, It.Is<string>(t => t.Contains("Bob")), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Digest_BuildDigestTextAsync_IgnoresEnableFlag()
    {
        var settings = new Mock<ISettingsService>(MockBehavior.Strict);
        var overview = new Mock<IFreeTierEnforcementOverviewService>();
        overview.Setup(o => o.GetUnsubscribedConnectedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetFreeTierEnforcementCandidatesResponse
            {
                Candidates = [],
                TotalCount = 0,
                ConnectedCount = 0,
            });

        var sut = new FreeTierUnsubscribedVpnUsersDailyDigestService(
            settings.Object,
            overview.Object,
            Mock.Of<ITelegramUserService>(),
            Mock.Of<ITelegramDirectMessageSender>(),
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<ILogger<FreeTierUnsubscribedVpnUsersDailyDigestService>>());

        var text = await sut.BuildDigestTextAsync(CancellationToken.None);

        Assert.Contains("Currently online: 0", text);
        overview.Verify(o => o.GetUnsubscribedConnectedAsync(It.IsAny<CancellationToken>()), Times.Once);
        settings.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Digest_WhenAlreadySentToday_Skips()
    {
        var settings = new Mock<ISettingsService>();
        SetupBoolSetting(settings, FreeTierAccessSettingsKeys.DailyUnsubscribedAdminDigest, true);

        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("free-tier-unsub-admin-digest:last-utc-date", DateOnly.FromDateTime(DateTime.UtcNow));

        var overview = new Mock<IFreeTierEnforcementOverviewService>(MockBehavior.Strict);
        var sut = new FreeTierUnsubscribedVpnUsersDailyDigestService(
            settings.Object,
            overview.Object,
            Mock.Of<ITelegramUserService>(),
            Mock.Of<ITelegramDirectMessageSender>(),
            cache,
            Mock.Of<ILogger<FreeTierUnsubscribedVpnUsersDailyDigestService>>());

        await sut.TrySendDailyDigestAsync(CancellationToken.None);
        overview.VerifyNoOtherCalls();
    }
}
