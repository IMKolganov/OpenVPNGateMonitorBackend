using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.TelegramBot.Interfaces;
using DataGateMonitor.Services.Users;
using DataGateMonitor.Services.Users.Interfaces;
using DataGateMonitor.SharedModels.DataGateMonitor.User.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.User.Responses;
using Xunit;

namespace DataGateMonitor.Tests.Services.Users;

public class TelegramAccountLinkServiceTests
{
    private const long DefaultTelegramId = 999;

    [Fact]
    public async Task RequestLinkCodeAsync_WithoutTelegramId_ReturnsCodeForBotCompletion()
    {
        var merge = new Mock<IUserMergeService>();
        merge.Setup(m => m.MergeTelegramGoogleAsync(
                It.IsAny<MergeTelegramGoogleUsersRequest>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MergeTelegramGoogleUsersResponse { SurvivorUserId = 10, MergedUserId = 10 });

        var sut = CreateSut(
            userId: 20,
            telegramUserId: 10,
            links: [new UserIdentityLink { Provider = AuthIdentityProviders.Google, ExternalId = "sub" }],
            merge: merge.Object);

        var result = await sut.RequestLinkCodeAsync(20, null, CancellationToken.None);

        Assert.Equal(8, result.Code.Length);

        var completed = await sut.CompleteLinkByCodeAsync(result.Code, DefaultTelegramId, CancellationToken.None);
        Assert.True(completed.Success);
    }

    [Fact]
    public async Task RequestLinkCodeFromBot_AndCompleteFromApp_LinksAccounts()
    {
        var merge = new Mock<IUserMergeService>();
        merge.Setup(m => m.MergeTelegramGoogleAsync(
                It.Is<MergeTelegramGoogleUsersRequest>(r => r.TelegramUserId == 10 && r.GoogleUserId == 20),
                20,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MergeTelegramGoogleUsersResponse { SurvivorUserId = 10, MergedUserId = 20 });

        var adminAlert = new Mock<ITelegramAdminAlertService>();
        adminAlert
            .Setup(a => a.NotifyAccountsLinkedAsync(It.IsAny<TelegramAccountsLinkedAlert>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(
            userId: 20,
            telegramUserId: 10,
            links: [new UserIdentityLink { Provider = AuthIdentityProviders.Google, ExternalId = "g-sub" }],
            merge: merge.Object,
            adminAlert: adminAlert);

        var issued = await sut.RequestLinkCodeFromBotAsync(DefaultTelegramId, CancellationToken.None);
        var result = await sut.CompleteLinkFromAppAsync(20, issued.Code, CancellationToken.None);

        Assert.True(result.Success);
        merge.Verify(
            m => m.MergeTelegramGoogleAsync(
                It.IsAny<MergeTelegramGoogleUsersRequest>(),
                20,
                It.IsAny<CancellationToken>()),
            Times.Once);
        adminAlert.Verify(
            a => a.NotifyAccountsLinkedAsync(
                It.Is<TelegramAccountsLinkedAlert>(x =>
                    x.TelegramId == DefaultTelegramId
                    && x.LinkedProviderLabel == "Google"
                    && x.SurvivorUserId == 10
                    && x.MergedUserId == 20),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CompleteLinkFromApp_WhenCodeWasForBot_ReturnsFailure()
    {
        var sut = CreateSut(
            userId: 10,
            links: [new UserIdentityLink { Provider = AuthIdentityProviders.Google, ExternalId = "sub" }]);

        var issued = await sut.RequestLinkCodeAsync(10, null, CancellationToken.None);
        var result = await sut.CompleteLinkFromAppAsync(10, issued.Code, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Telegram bot", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RequestLinkCodeAsync_WhenUserHasGoogleLink_ReturnsCode()
    {
        var sut = CreateSut(
            userId: 10,
            links: [new UserIdentityLink { Provider = AuthIdentityProviders.Google, ExternalId = "sub" }]);

        var result = await sut.RequestLinkCodeAsync(10, DefaultTelegramId, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.Code));
        Assert.Equal(8, result.Code.Length);
        Assert.True(result.ExpiresInSeconds > 0);
    }

    [Fact]
    public async Task RequestLinkCodeAsync_WhenAlreadyLinkedToTelegram_Throws()
    {
        var sut = CreateSut(
            userId: 5,
            links:
            [
                new UserIdentityLink { Provider = AuthIdentityProviders.Telegram, ExternalId = "123" },
                new UserIdentityLink { Provider = AuthIdentityProviders.Google, ExternalId = "sub" },
            ]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RequestLinkCodeAsync(5, DefaultTelegramId, CancellationToken.None));

        Assert.Contains("already linked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RequestLinkCodeAsync_WhenUserHasLocalLink_ReturnsCode()
    {
        var sut = CreateSut(
            userId: 6,
            links: [new UserIdentityLink { Provider = AuthIdentityProviders.Local, ExternalId = "6" }]);

        var result = await sut.RequestLinkCodeAsync(6, DefaultTelegramId, CancellationToken.None);

        Assert.Equal(8, result.Code.Length);
    }

    [Fact]
    public async Task RequestLinkCodeAsync_WhenNoGoogleOrLocalLink_Throws()
    {
        var sut = CreateSut(userId: 7, links: []);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RequestLinkCodeAsync(7, DefaultTelegramId, CancellationToken.None));

        Assert.Contains("Google or password", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RequestLinkCodeAsync_WhenTelegramNotRegistered_Throws()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var userQuery = new Mock<IUserQueryService>();
        var linkQuery = new Mock<IUserIdentityLinkQueryService>();
        var merge = new Mock<IUserMergeService>();
        var config = new Mock<IConfiguration>();
        config.Setup(c => c.GetSection(It.IsAny<string>())).Returns(Mock.Of<IConfigurationSection>());

        userQuery.Setup(q => q.GetById(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 8, IsBlocked = false });
        linkQuery.Setup(q => q.GetListByUserId(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new UserIdentityLink { Provider = AuthIdentityProviders.Google, ExternalId = "sub" }]);
        linkQuery.Setup(q => q.GetByProviderAndExternalId(
                AuthIdentityProviders.Telegram,
                DefaultTelegramId.ToString(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserIdentityLink?)null);

        var sut = new TelegramAccountLinkService(
            cache,
            userQuery.Object,
            linkQuery.Object,
            merge.Object,
            Mock.Of<ITelegramUserService>(),
            Mock.Of<ITelegramAdminAlertService>(),
            config.Object,
            Mock.Of<ILogger<TelegramAccountLinkService>>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RequestLinkCodeAsync(8, DefaultTelegramId, CancellationToken.None));

        Assert.Contains("not registered", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RequestLinkCodeAsync_InvalidatesPreviousCode()
    {
        var sut = CreateSut(userId: 10, links:
        [
            new UserIdentityLink { Provider = AuthIdentityProviders.Google, ExternalId = "sub" },
        ]);

        var first = await sut.RequestLinkCodeAsync(10, DefaultTelegramId, CancellationToken.None);
        _ = await sut.RequestLinkCodeAsync(10, DefaultTelegramId, CancellationToken.None);

        var firstResult = await sut.CompleteLinkByCodeAsync(first.Code, DefaultTelegramId, CancellationToken.None);
        Assert.False(firstResult.Success);
        Assert.Contains("Invalid or expired", firstResult.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompleteLinkByCodeAsync_WhenCodeInvalid_ReturnsFailure()
    {
        var sut = CreateSut(userId: 1, links: []);

        var result = await sut.CompleteLinkByCodeAsync("BADCODE1", 12345, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Invalid or expired", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompleteLinkByCodeAsync_WhenTelegramIdMismatch_ReturnsFailureAndAllowsCorrectId()
    {
        var merge = new Mock<IUserMergeService>();
        merge.Setup(m => m.MergeTelegramGoogleAsync(
                It.IsAny<MergeTelegramGoogleUsersRequest>(),
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MergeTelegramGoogleUsersResponse { SurvivorUserId = 10, MergedUserId = 20 });

        var sut = CreateSut(
            userId: 20,
            telegramUserId: 10,
            links: [new UserIdentityLink { Provider = AuthIdentityProviders.Google, ExternalId = "g-sub" }],
            merge: merge.Object,
            registeredTelegramId: 111);

        var issued = await sut.RequestLinkCodeAsync(20, 111, CancellationToken.None);

        var wrong = await sut.CompleteLinkByCodeAsync(issued.Code, DefaultTelegramId, CancellationToken.None);
        Assert.False(wrong.Success);
        Assert.Contains("different Telegram account", wrong.Message, StringComparison.OrdinalIgnoreCase);

        var correct = await sut.CompleteLinkByCodeAsync(issued.Code, 111, CancellationToken.None);
        Assert.True(correct.Success);
    }

    [Fact]
    public async Task CompleteLinkByCodeAsync_WhenSurvivorHasDifferentGoogle_ReturnsLinkedAccountMessage()
    {
        const int survivorUserId = 53;
        const int dashboardUserId = 117;

        var merge = new Mock<IUserMergeService>(MockBehavior.Strict);

        var sut = CreateSut(
            userId: dashboardUserId,
            telegramUserId: survivorUserId,
            links:
            [
                new UserIdentityLink
                {
                    UserId = dashboardUserId,
                    Provider = AuthIdentityProviders.Google,
                    ExternalId = "117358012580610142000",
                },
            ],
            survivorLinks:
            [
                new UserIdentityLink
                {
                    UserId = survivorUserId,
                    Provider = AuthIdentityProviders.Telegram,
                    ExternalId = DefaultTelegramId.ToString(),
                },
                new UserIdentityLink
                {
                    UserId = survivorUserId,
                    Provider = AuthIdentityProviders.Google,
                    ExternalId = "101156788354753647270",
                },
            ],
            survivorUser: new User
            {
                Id = survivorUserId,
                DisplayName = "koz_nik",
                Email = "25052001kozin@gmail.com",
            },
            merge: merge.Object);

        var issued = await sut.RequestLinkCodeAsync(dashboardUserId, null, CancellationToken.None);
        var result = await sut.CompleteLinkByCodeAsync(issued.Code, DefaultTelegramId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.StartsWith(TelegramAccountLinkService.TelegramAlreadyLinkedToGooglePrefix, result.Message, StringComparison.Ordinal);
        Assert.Contains("25052001kozin@gmail.com", result.Message, StringComparison.Ordinal);
        Assert.Contains("koz_nik", result.Message, StringComparison.Ordinal);
        merge.Verify(
            m => m.MergeTelegramGoogleAsync(
                It.IsAny<MergeTelegramGoogleUsersRequest>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CompleteLinkByCodeAsync_WhenMergeFails_CodeRemainsValid()
    {
        var merge = new Mock<IUserMergeService>();
        merge.Setup(m => m.MergeTelegramGoogleAsync(
                It.IsAny<MergeTelegramGoogleUsersRequest>(),
                10,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("merge failed"));

        var sut = CreateSut(
            userId: 20,
            telegramUserId: 10,
            links: [new UserIdentityLink { Provider = AuthIdentityProviders.Google, ExternalId = "g-sub" }],
            merge: merge.Object);

        var issued = await sut.RequestLinkCodeAsync(20, DefaultTelegramId, CancellationToken.None);
        var first = await sut.CompleteLinkByCodeAsync(issued.Code, DefaultTelegramId, CancellationToken.None);
        var second = await sut.CompleteLinkByCodeAsync(issued.Code, DefaultTelegramId, CancellationToken.None);

        Assert.False(first.Success);
        Assert.False(second.Success);
        Assert.Contains("try again", first.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompleteLinkByCodeAsync_WhenValid_CallsMergeAndRemovesCode()
    {
        var merge = new Mock<IUserMergeService>();
        merge.Setup(m => m.MergeTelegramGoogleAsync(
                It.Is<MergeTelegramGoogleUsersRequest>(r => r.TelegramUserId == 10 && r.GoogleUserId == 20),
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MergeTelegramGoogleUsersResponse
            {
                SurvivorUserId = 10,
                MergedUserId = 20,
            });

        var sut = CreateSut(
            userId: 20,
            telegramUserId: 10,
            links: [new UserIdentityLink { Provider = AuthIdentityProviders.Google, ExternalId = "g-sub" }],
            merge: merge.Object);

        var issued = await sut.RequestLinkCodeAsync(20, DefaultTelegramId, CancellationToken.None);
        var result = await sut.CompleteLinkByCodeAsync(issued.Code, DefaultTelegramId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Merge);

        var reuse = await sut.CompleteLinkByCodeAsync(issued.Code, DefaultTelegramId, CancellationToken.None);
        Assert.False(reuse.Success);
        Assert.Contains("Invalid or expired", reuse.Message, StringComparison.OrdinalIgnoreCase);

        merge.Verify(
            m => m.MergeTelegramGoogleAsync(
                It.IsAny<MergeTelegramGoogleUsersRequest>(),
                10,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CompleteLinkByCodeAsync_WhenCodeAlreadyRedeemedAndSurvivorIsMerged_ReturnsAlreadyLinkedSuccess()
    {
        const int survivorUserId = 10;
        var cache = new MemoryCache(new MemoryCacheOptions());
        var userQuery = new Mock<IUserQueryService>();
        var linkQuery = new Mock<IUserIdentityLinkQueryService>();
        var config = new Mock<IConfiguration>();
        config.Setup(c => c.GetSection(It.IsAny<string>())).Returns(Mock.Of<IConfigurationSection>());

        // Code is not in the cache (already redeemed by an earlier successful call and removed).
        linkQuery.Setup(q => q.GetByProviderAndExternalId(
                AuthIdentityProviders.Telegram, DefaultTelegramId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserIdentityLink { UserId = survivorUserId, Provider = AuthIdentityProviders.Telegram, ExternalId = DefaultTelegramId.ToString() });
        linkQuery.Setup(q => q.GetListByUserId(survivorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new UserIdentityLink { UserId = survivorUserId, Provider = AuthIdentityProviders.Telegram, ExternalId = DefaultTelegramId.ToString() },
                new UserIdentityLink { UserId = survivorUserId, Provider = AuthIdentityProviders.Google, ExternalId = "g-sub" },
            ]);

        var sut = new TelegramAccountLinkService(
            cache, userQuery.Object, linkQuery.Object, Mock.Of<IUserMergeService>(),
            Mock.Of<ITelegramUserService>(), Mock.Of<ITelegramAdminAlertService>(),
            config.Object,
            Mock.Of<ILogger<TelegramAccountLinkService>>());

        var result = await sut.CompleteLinkByCodeAsync("STALECOD", DefaultTelegramId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("already linked", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompleteLinkByCodeAsync_WhenCodeAlreadyRedeemedAndSurvivorNotMerged_ReturnsInvalidCode()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var userQuery = new Mock<IUserQueryService>();
        var linkQuery = new Mock<IUserIdentityLinkQueryService>();
        var config = new Mock<IConfiguration>();
        config.Setup(c => c.GetSection(It.IsAny<string>())).Returns(Mock.Of<IConfigurationSection>());

        linkQuery.Setup(q => q.GetByProviderAndExternalId(
                AuthIdentityProviders.Telegram, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserIdentityLink?)null);

        var sut = new TelegramAccountLinkService(
            cache, userQuery.Object, linkQuery.Object, Mock.Of<IUserMergeService>(),
            Mock.Of<ITelegramUserService>(), Mock.Of<ITelegramAdminAlertService>(),
            config.Object,
            Mock.Of<ILogger<TelegramAccountLinkService>>());

        var result = await sut.CompleteLinkByCodeAsync("NOSUCHCD", DefaultTelegramId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Invalid or expired", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompleteLinkByCodeAsync_WhenDashboardUserGoneButSurvivorMerged_ReturnsAlreadyLinkedSuccess()
    {
        const int dashboardUserId = 20;
        const int survivorUserId = 10;
        var cache = new MemoryCache(new MemoryCacheOptions());
        var userQuery = new Mock<IUserQueryService>();
        var linkQuery = new Mock<IUserIdentityLinkQueryService>();
        var config = new Mock<IConfiguration>();
        config.Setup(c => c.GetSection(It.IsAny<string>())).Returns(Mock.Of<IConfigurationSection>());

        // Dashboard user existed when the code was requested, but is gone (hard-deleted by an
        // earlier successful merge) by the time this call resolves it.
        userQuery.SetupSequence(q => q.GetById(dashboardUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = dashboardUserId, IsBlocked = false })
            .ReturnsAsync((User?)null);

        linkQuery.Setup(q => q.GetListByUserId(dashboardUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new UserIdentityLink { UserId = dashboardUserId, Provider = AuthIdentityProviders.Google, ExternalId = "g-sub" }]);
        linkQuery.Setup(q => q.GetByProviderAndExternalId(
                AuthIdentityProviders.Telegram, DefaultTelegramId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserIdentityLink { UserId = survivorUserId, Provider = AuthIdentityProviders.Telegram, ExternalId = DefaultTelegramId.ToString() });
        linkQuery.Setup(q => q.GetListByUserId(survivorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new UserIdentityLink { UserId = survivorUserId, Provider = AuthIdentityProviders.Telegram, ExternalId = DefaultTelegramId.ToString() },
                new UserIdentityLink { UserId = survivorUserId, Provider = AuthIdentityProviders.Google, ExternalId = "g-sub" },
            ]);

        var sut = new TelegramAccountLinkService(
            cache, userQuery.Object, linkQuery.Object, Mock.Of<IUserMergeService>(),
            Mock.Of<ITelegramUserService>(), Mock.Of<ITelegramAdminAlertService>(),
            config.Object,
            Mock.Of<ILogger<TelegramAccountLinkService>>());

        var issued = await sut.RequestLinkCodeAsync(dashboardUserId, null, CancellationToken.None);
        var result = await sut.CompleteLinkByCodeAsync(issued.Code, DefaultTelegramId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("already linked", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static TelegramAccountLinkService CreateSut(
        int userId,
        IReadOnlyList<UserIdentityLink> links,
        long registeredTelegramId = DefaultTelegramId,
        int telegramUserId = 0,
        IReadOnlyList<UserIdentityLink>? survivorLinks = null,
        User? survivorUser = null,
        IMemoryCache? cache = null,
        IUserMergeService? merge = null,
        Mock<ITelegramAdminAlertService>? adminAlert = null)
    {
        cache ??= new MemoryCache(new MemoryCacheOptions());
        var userQuery = new Mock<IUserQueryService>();
        var linkQuery = new Mock<IUserIdentityLinkQueryService>();
        var mergeMock = merge is null ? new Mock<IUserMergeService>() : null;
        var mergeService = merge ?? mergeMock!.Object;
        var config = new Mock<IConfiguration>();
        config.Setup(c => c.GetSection(It.IsAny<string>())).Returns(Mock.Of<IConfigurationSection>());

        adminAlert ??= new Mock<ITelegramAdminAlertService>();
        adminAlert
            .Setup(a => a.NotifyAccountsLinkedAsync(It.IsAny<TelegramAccountsLinkedAlert>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var telegramUsers = new Mock<ITelegramUserService>();
        telegramUsers
            .Setup(s => s.GetUserByTelegramIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TelegramBotUser?)null);

        var resolvedTelegramUserId = telegramUserId > 0 ? telegramUserId : userId;
        var resolvedSurvivorLinks = survivorLinks ??
        [
            new UserIdentityLink
            {
                UserId = resolvedTelegramUserId,
                Provider = AuthIdentityProviders.Telegram,
                ExternalId = registeredTelegramId.ToString(),
            },
        ];

        userQuery.Setup(q => q.GetById(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = userId,
                IsBlocked = false,
                DisplayName = $"user-{userId}",
                Email = $"user{userId}@example.com",
                AvatarUrl = "https://example.com/avatar.jpg",
            });
        if (resolvedTelegramUserId != userId)
        {
            userQuery.Setup(q => q.GetById(resolvedTelegramUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(survivorUser ?? new User { Id = resolvedTelegramUserId, IsBlocked = false });
        }

        linkQuery.Setup(q => q.GetListByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(links.ToList());
        if (resolvedTelegramUserId != userId)
        {
            linkQuery.Setup(q => q.GetListByUserId(resolvedTelegramUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(resolvedSurvivorLinks.ToList());
        }
        linkQuery.Setup(q => q.GetByProviderAndExternalId(
                AuthIdentityProviders.Telegram,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string externalId, CancellationToken _) =>
            {
                if (!string.Equals(externalId, registeredTelegramId.ToString(), StringComparison.Ordinal))
                    return null;

                return new UserIdentityLink
                {
                    UserId = resolvedTelegramUserId,
                    Provider = AuthIdentityProviders.Telegram,
                    ExternalId = externalId,
                };
            });

        return new TelegramAccountLinkService(
            cache,
            userQuery.Object,
            linkQuery.Object,
            mergeService,
            telegramUsers.Object,
            adminAlert.Object,
            config.Object,
            Mock.Of<ILogger<TelegramAccountLinkService>>());
    }
}
