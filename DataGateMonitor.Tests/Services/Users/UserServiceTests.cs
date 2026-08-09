using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.TelegramBotUserTable;
using DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Others.Notifications;
using DataGateMonitor.Services.Users;
using DataGateMonitor.SharedModels.DataGateMonitor.User.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.User.Responses;
using DataGateMonitor.SharedModels.DataGateMonitor.User.Responses.Dto;
using DataGateMonitor.SharedModels.Responses;
using Xunit;

namespace DataGateMonitor.Tests.Services.Users;

public class UserServiceTests
{
    private readonly Mock<IUserQueryService> _userQuery;
    private readonly Mock<ITelegramBotUserQueryService> _telegramBotUserQuery;
    private readonly Mock<IUserIdentityLinkQueryService> _userIdentityLinkQuery;
    private readonly Mock<IQuotaPlanQueryService> _quotaPlanQuery;
    private readonly Mock<ICommandService<User, int>> _userCommand;
    private readonly Mock<ICommandService<UserIdentityLink, int>> _userIdentityLinkCommand;
    private readonly Mock<ICommandService<UserQuotaPlan, int>> _userQuotaPlanCommand;
    private readonly Mock<ICommandService<TelegramBotUser, int>> _telegramBotUserCommand;
    private readonly Mock<IAppNotificationFacade> _appNotificationFacade;
    private readonly Mock<ILogger<UserService>> _logger;
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _userQuery = new Mock<IUserQueryService>(MockBehavior.Strict);
        _telegramBotUserQuery = new Mock<ITelegramBotUserQueryService>(MockBehavior.Strict);
        _userIdentityLinkQuery = new Mock<IUserIdentityLinkQueryService>(MockBehavior.Strict);
        _quotaPlanQuery = new Mock<IQuotaPlanQueryService>(MockBehavior.Strict);
        _userCommand = new Mock<ICommandService<User, int>>(MockBehavior.Strict);
        _userIdentityLinkCommand = new Mock<ICommandService<UserIdentityLink, int>>(MockBehavior.Strict);
        _userQuotaPlanCommand = new Mock<ICommandService<UserQuotaPlan, int>>(MockBehavior.Strict);
        _telegramBotUserCommand = new Mock<ICommandService<TelegramBotUser, int>>(MockBehavior.Strict);
        _appNotificationFacade = new Mock<IAppNotificationFacade>(MockBehavior.Loose);
        _logger = new Mock<ILogger<UserService>>(MockBehavior.Loose);

        _sut = new UserService(
            _userQuery.Object,
            _telegramBotUserQuery.Object,
            _userIdentityLinkQuery.Object,
            _quotaPlanQuery.Object,
            _userCommand.Object,
            _userIdentityLinkCommand.Object,
            _userQuotaPlanCommand.Object,
            _telegramBotUserCommand.Object,
            _appNotificationFacade.Object,
            _logger.Object);
    }

    [Fact]
    public async Task GetUserById_WhenUserExists_ReturnsUsersResponse()
    {
        var request = new GetUserByIdRequest { Id = 42 };
        var user = new User
        {
            Id = 42,
            DisplayName = "Test User",
            Email = null,
            IsAdmin = false,
            IsBlocked = false,
            HasDashboardAccess = false,
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow
        };
        var link = new UserIdentityLink
        {
            Id = 1,
            UserId = 42,
            Provider = "telegram",
            ExternalId = "12345",
            ProviderRowId = 10,
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow
        };

        _userQuery.Setup(q => q.GetById(42, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _userIdentityLinkQuery.Setup(q => q.GetByUserId(42, It.IsAny<CancellationToken>())).ReturnsAsync(link);

        var result = await _sut.GetUserById(request, CancellationToken.None);

        result.Should().NotBeNull();
        result.User.Should().NotBeNull();
        result.User!.Id.Should().Be(42);
        result.User.DisplayName.Should().Be("Test User");
        result.User.Provider.Should().Be("telegram");
        result.User.ExternalId.Should().Be("12345");
        result.User.ProviderRowId.Should().Be(10);
        _userQuery.VerifyAll();
        _userIdentityLinkQuery.VerifyAll();
    }

    [Fact]
    public async Task GetUserById_WhenUserNotFound_ThrowsKeyNotFoundException()
    {
        var request = new GetUserByIdRequest { Id = 999 };
        _userQuery.Setup(q => q.GetById(999, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var act = () => _sut.GetUserById(request, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*User 999 not found*");
        _userQuery.VerifyAll();
    }

    [Fact]
    public async Task GetUsersPage_ReturnsPagedUsersAsDtos()
    {
        var users = new List<User>
        {
            new() { Id = 1, DisplayName = "U1", CreateDate = DateTimeOffset.UtcNow, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = 2, DisplayName = "U2", CreateDate = DateTimeOffset.UtcNow, LastUpdate = DateTimeOffset.UtcNow }
        };
        var paged = new PagedResponse<User>
        {
            Page = 1,
            PageSize = 20,
            TotalCount = 2,
            Items = users
        };
        _userQuery.Setup(q => q.GetPage(It.Is<GetAllUsersRequest>(r => r.Page == 1 && r.PageSize == 20), It.IsAny<CancellationToken>())).ReturnsAsync(paged);
        _userIdentityLinkQuery
            .Setup(q => q.GetFirstByUserIds(
                It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 1, 2 })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, UserIdentityLink>());

        var result = await _sut.GetUsersPage(new GetAllUsersRequest { Page = 1, PageSize = 20 }, CancellationToken.None);

        result.Should().NotBeNull();
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.TotalCount.Should().Be(2);
        result.Users.Should().HaveCount(2);
        result.Users![0].Id.Should().Be(1);
        result.Users[0].DisplayName.Should().Be("U1");
        result.Users[0].Provider.Should().BeEmpty();
        result.Users[0].ExternalId.Should().BeEmpty();
        result.Users[0].ProviderRowId.Should().BeNull();
        result.Users[1].Id.Should().Be(2);
        result.Users[1].DisplayName.Should().Be("U2");
        _userQuery.VerifyAll();
        _userIdentityLinkQuery.Verify(
            q => q.GetFirstByUserIds(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _userIdentityLinkQuery.Verify(
            q => q.GetByUserId(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetUsersPage_EnrichesDtosFromBatchedIdentityLinks()
    {
        var users = new List<User>
        {
            new() { Id = 1, DisplayName = "U1", CreateDate = DateTimeOffset.UtcNow, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = 2, DisplayName = "U2", CreateDate = DateTimeOffset.UtcNow, LastUpdate = DateTimeOffset.UtcNow }
        };
        var paged = new PagedResponse<User>
        {
            Page = 1,
            PageSize = 20,
            TotalCount = 2,
            Items = users
        };
        var links = new Dictionary<int, UserIdentityLink>
        {
            [1] = new()
            {
                Id = 10,
                UserId = 1,
                Provider = "google",
                ExternalId = "g-1",
                ProviderRowId = 55,
                CreateDate = DateTimeOffset.UtcNow,
                LastUpdate = DateTimeOffset.UtcNow
            }
        };
        _userQuery.Setup(q => q.GetPage(It.IsAny<GetAllUsersRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(paged);
        _userIdentityLinkQuery
            .Setup(q => q.GetFirstByUserIds(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(links);

        var result = await _sut.GetUsersPage(new GetAllUsersRequest { Page = 1, PageSize = 20 }, CancellationToken.None);

        result.Users![0].Provider.Should().Be("google");
        result.Users[0].ExternalId.Should().Be("g-1");
        result.Users[0].ProviderRowId.Should().Be(55);
        result.Users[1].Provider.Should().BeEmpty();
        result.Users[1].ExternalId.Should().BeEmpty();
        result.Users[1].ProviderRowId.Should().BeNull();
        _userIdentityLinkQuery.Verify(
            q => q.GetFirstByUserIds(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUserByExternalId_WhenLinkExists_ReturnsUsersResponse()
    {
        var request = new GetUserByExternalIdRequest { ExternalId = "ext-99" };
        var link = new UserIdentityLink
        {
            Id = 1,
            UserId = 7,
            Provider = "google",
            ExternalId = "ext-99",
            ProviderRowId = null,
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow
        };
        var user = new User
        {
            Id = 7,
            DisplayName = "Google User",
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow
        };

        _userIdentityLinkQuery.Setup(q => q.GetByExternalId("ext-99", It.IsAny<CancellationToken>())).ReturnsAsync(link);
        _userQuery.Setup(q => q.GetById(7, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _userIdentityLinkQuery.Setup(q => q.GetByUserId(7, It.IsAny<CancellationToken>())).ReturnsAsync(link);

        var result = await _sut.GetUserByExternalId(request, CancellationToken.None);

        result.Should().NotBeNull();
        result.User!.Id.Should().Be(7);
        result.User.DisplayName.Should().Be("Google User");
        result.User.Provider.Should().Be("google");
        result.User.ExternalId.Should().Be("ext-99");
        _userIdentityLinkQuery.VerifyAll();
        _userQuery.VerifyAll();
    }

    [Fact]
    public async Task GetUserByExternalId_WhenLinkNotFound_ThrowsKeyNotFoundException()
    {
        var request = new GetUserByExternalIdRequest { ExternalId = "missing" };
        _userIdentityLinkQuery.Setup(q => q.GetByExternalId("missing", It.IsAny<CancellationToken>())).ReturnsAsync((UserIdentityLink?)null);

        var act = () => _sut.GetUserByExternalId(request, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Identity link not found*externalId 'missing'*");
        _userIdentityLinkQuery.VerifyAll();
    }

    [Fact]
    public async Task RegisterUserFromTgBot_WhenTelegramUserAndLinkExist_UpdatesTelegramUserAndReturnsResponse()
    {
        var request = new RegisterUserFromTgBotRequest
        {
            TelegramId = 12345,
            Username = "updated_user",
            FirstName = "Up",
            LastName = "User"
        };
        var ct = CancellationToken.None;
        var existingTgUser = new TelegramBotUser
        {
            Id = 10,
            TelegramId = 12345,
            Username = "old",
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow
        };
        var link = new UserIdentityLink
        {
            Id = 1,
            UserId = 5,
            Provider = "telegram",
            ExternalId = "12345",
            ProviderRowId = 10,
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow
        };
        var user = new User
        {
            Id = 5,
            DisplayName = "User",
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow
        };

        _telegramBotUserQuery.Setup(q => q.GetByTelegramId(12345, ct)).ReturnsAsync(existingTgUser);
        _telegramBotUserCommand.Setup(c => c.Update(It.IsAny<TelegramBotUser>(), true, ct)).ReturnsAsync(1);
        _userIdentityLinkQuery.Setup(q => q.GetByProviderAndExternalId("telegram", "12345", ct)).ReturnsAsync(link);
        _userQuery.Setup(q => q.GetById(5, ct)).ReturnsAsync(user);
        _userIdentityLinkQuery.Setup(q => q.GetByUserId(5, ct)).ReturnsAsync(link);

        var result = await _sut.RegisterUserFromTgBot(request, ct);

        result.Should().NotBeNull();
        result.User!.Id.Should().Be(5);
        result.User.Provider.Should().Be("telegram");
        result.User.ExternalId.Should().Be("12345");
        result.User.ProviderRowId.Should().Be(10);
        _telegramBotUserCommand.Verify(c => c.Update(It.IsAny<TelegramBotUser>(), true, ct), Times.Once);
        _userCommand.Verify(c => c.Add(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        _appNotificationFacade.Verify(
            f => f.UserRegistered(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterUserFromTgBot_WhenNewTelegramUserAndNoLink_CreatesUserLinkAndAssignsDefaultQuota()
    {
        var request = new RegisterUserFromTgBotRequest
        {
            TelegramId = 99999,
            Username = "newbie",
            FirstName = "New",
            LastName = "User"
        };
        var ct = CancellationToken.None;
        var now = DateTimeOffset.UtcNow;
        var defaultPlan = new QuotaPlan
        {
            Id = 1,
            Name = "Default",
            CreateDate = now,
            LastUpdate = now
        };

        _telegramBotUserQuery.Setup(q => q.GetByTelegramId(99999, ct)).ReturnsAsync((TelegramBotUser?)null);
        _telegramBotUserCommand
            .Setup(c => c.Add(It.IsAny<TelegramBotUser>(), true, ct))
            .Returns((TelegramBotUser e, bool _, CancellationToken _) =>
                Task.FromResult(new TelegramBotUser { Id = 100, TelegramId = e.TelegramId, Username = e.Username, CreateDate = now, LastUpdate = now }));
        _userIdentityLinkQuery.Setup(q => q.GetByProviderAndExternalId("telegram", "99999", ct)).ReturnsAsync((UserIdentityLink?)null);
        _userCommand
            .Setup(c => c.Add(It.IsAny<User>(), true, ct))
            .Returns((User e, bool _, CancellationToken _) =>
                Task.FromResult(new User { Id = 50, DisplayName = e.DisplayName, CreateDate = e.CreateDate, LastUpdate = e.LastUpdate }));
        _userIdentityLinkCommand
            .Setup(c => c.Add(It.IsAny<UserIdentityLink>(), true, ct))
            .Returns((UserIdentityLink e, bool _, CancellationToken _) => Task.FromResult(e));
        _quotaPlanQuery.Setup(q => q.GetDefault(ct)).ReturnsAsync(defaultPlan);
        _userQuotaPlanCommand
            .Setup(c => c.Add(It.IsAny<UserQuotaPlan>(), true, ct))
            .Returns((UserQuotaPlan e, bool _, CancellationToken _) => Task.FromResult(e));
        _userIdentityLinkQuery.Setup(q => q.GetByUserId(50, ct)).ReturnsAsync((UserIdentityLink?)null);

        var result = await _sut.RegisterUserFromTgBot(request, ct);

        result.Should().NotBeNull();
        result.User!.DisplayName.Should().Be("newbie");
        result.User.Provider.Should().Be("telegram");
        result.User.ExternalId.Should().Be("99999");
        result.User.ProviderRowId.Should().Be(100);
        _telegramBotUserCommand.Verify(c => c.Add(It.IsAny<TelegramBotUser>(), true, ct), Times.Once);
        _userCommand.Verify(c => c.Add(It.IsAny<User>(), true, ct), Times.Once);
        _userIdentityLinkCommand.Verify(c => c.Add(It.IsAny<UserIdentityLink>(), true, ct), Times.Once);
        _userQuotaPlanCommand.Verify(c => c.Add(It.IsAny<UserQuotaPlan>(), true, ct), Times.Once);
        _appNotificationFacade.Verify(
            f => f.UserRegistered(50, "newbie", null, null, "Telegram bot", ct),
            Times.Once);
    }

    [Fact]
    public async Task RegisterUserFromTgBot_WhenNewTelegramUserNoLinkNoDefaultQuota_StillCreatesUserAndLink()
    {
        var request = new RegisterUserFromTgBotRequest { TelegramId = 88888, Username = "minimal" };
        var ct = CancellationToken.None;

        _telegramBotUserQuery.Setup(q => q.GetByTelegramId(88888, ct)).ReturnsAsync((TelegramBotUser?)null);
        _telegramBotUserCommand
            .Setup(c => c.Add(It.IsAny<TelegramBotUser>(), true, ct))
            .Returns((TelegramBotUser e, bool _, CancellationToken _) =>
                Task.FromResult(new TelegramBotUser { Id = 200, TelegramId = e.TelegramId, CreateDate = DateTimeOffset.UtcNow, LastUpdate = DateTimeOffset.UtcNow }));
        _userIdentityLinkQuery.Setup(q => q.GetByProviderAndExternalId("telegram", "88888", ct)).ReturnsAsync((UserIdentityLink?)null);
        _userCommand
            .Setup(c => c.Add(It.IsAny<User>(), true, ct))
            .Returns((User e, bool _, CancellationToken _) =>
                Task.FromResult(new User { Id = 60, DisplayName = e.DisplayName ?? "tg_88888", CreateDate = e.CreateDate, LastUpdate = e.LastUpdate }));
        _userIdentityLinkCommand
            .Setup(c => c.Add(It.IsAny<UserIdentityLink>(), true, ct))
            .Returns((UserIdentityLink e, bool _, CancellationToken _) => Task.FromResult(e));
        _quotaPlanQuery.Setup(q => q.GetDefault(ct)).ReturnsAsync((QuotaPlan?)null);
        _userIdentityLinkQuery.Setup(q => q.GetByUserId(60, ct)).ReturnsAsync((UserIdentityLink?)null);

        var result = await _sut.RegisterUserFromTgBot(request, ct);

        result.Should().NotBeNull();
        result.User!.DisplayName.Should().Be("minimal");
        result.User.ExternalId.Should().Be("88888");
        _userQuotaPlanCommand.Verify(c => c.Add(It.IsAny<UserQuotaPlan>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetUsersPage_ClampsInvalidPageAndPageSize()
    {
        var paged = new PagedResponse<User>
        {
            Page = 1,
            PageSize = 500,
            TotalCount = 0,
            Items = [],
        };
        _userQuery.Setup(q => q.GetPage(It.Is<GetAllUsersRequest>(r => r.Page == 1 && r.PageSize == 500), It.IsAny<CancellationToken>())).ReturnsAsync(paged);
        _userIdentityLinkQuery
            .Setup(q => q.GetFirstByUserIds(
                It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 0),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, UserIdentityLink>());

        var result = await _sut.GetUsersPage(new GetAllUsersRequest { Page = 0, PageSize = 9999 }, CancellationToken.None);

        result.PageSize.Should().Be(500);
        _userQuery.Verify(q => q.GetPage(It.Is<GetAllUsersRequest>(r => r.Page == 1 && r.PageSize == 500), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUserByExternalId_WhenLinkedUserMissing_ThrowsInvalidOperationException()
    {
        var link = new UserIdentityLink { Id = 1, UserId = 404, Provider = "telegram", ExternalId = "123" };
        _userIdentityLinkQuery.Setup(q => q.GetByExternalId("123", It.IsAny<CancellationToken>())).ReturnsAsync(link);
        _userQuery.Setup(q => q.GetById(404, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var act = () => _sut.GetUserByExternalId(new GetUserByExternalIdRequest { ExternalId = "123" }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Linked user not found*404*");
    }

    [Fact]
    public async Task RegisterUserFromTgBot_WhenLinkedUserMissing_ThrowsInvalidOperationException()
    {
        var ct = CancellationToken.None;
        var tgUser = new TelegramBotUser { Id = 1, TelegramId = 555, CreateDate = DateTimeOffset.UtcNow, LastUpdate = DateTimeOffset.UtcNow };
        var link = new UserIdentityLink { Id = 2, UserId = 999, Provider = "telegram", ExternalId = "555" };

        _telegramBotUserQuery.Setup(q => q.GetByTelegramId(555, ct)).ReturnsAsync(tgUser);
        _telegramBotUserCommand.Setup(c => c.Update(It.IsAny<TelegramBotUser>(), true, ct)).ReturnsAsync(1);
        _userIdentityLinkQuery.Setup(q => q.GetByProviderAndExternalId("telegram", "555", ct)).ReturnsAsync(link);
        _userQuery.Setup(q => q.GetById(999, ct)).ReturnsAsync((User?)null);

        var act = () => _sut.RegisterUserFromTgBot(new RegisterUserFromTgBotRequest { TelegramId = 555 }, ct);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Linked user not found*999*");
    }

    [Fact]
    public async Task GetEmailConfirmationStatus_WhenUserExists_ReturnsStatus()
    {
        var user = new User { Id = 7, IsEmailConfirmed = false, CreateDate = DateTimeOffset.UtcNow, LastUpdate = DateTimeOffset.UtcNow };
        _userQuery.Setup(q => q.GetById(7, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await _sut.GetEmailConfirmationStatus(new GetUserEmailConfirmationStatusRequest { Id = 7 }, CancellationToken.None);

        result.IsEmailConfirmed.Should().BeFalse();
        _userQuery.VerifyAll();
    }

    [Fact]
    public async Task GetEmailConfirmationStatus_WhenUserMissing_ThrowsKeyNotFoundException()
    {
        _userQuery.Setup(q => q.GetById(404, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var act = () => _sut.GetEmailConfirmationStatus(new GetUserEmailConfirmationStatusRequest { Id = 404 }, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*User 404 not found*");
    }

    [Fact]
    public async Task ConfirmEmailManually_WhenUserMissing_ThrowsKeyNotFoundException()
    {
        _userQuery.Setup(q => q.GetById(404, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var act = () => _sut.ConfirmEmailManually(new ConfirmUserEmailRequest { Id = 404 }, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*User 404 not found*");
    }

    [Fact]
    public async Task ConfirmEmailManually_WhenUserHasNoEmail_ThrowsInvalidOperationException()
    {
        var user = new User { Id = 1, Email = null, CreateDate = DateTimeOffset.UtcNow, LastUpdate = DateTimeOffset.UtcNow };
        _userQuery.Setup(q => q.GetById(1, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var act = () => _sut.ConfirmEmailManually(new ConfirmUserEmailRequest { Id = 1 }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no email*");
    }

    [Fact]
    public async Task ConfirmEmailManually_WhenAlreadyConfirmed_ReturnsWithoutUpdate()
    {
        var user = new User
        {
            Id = 1,
            Email = "a@b.com",
            IsEmailConfirmed = true,
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow,
        };
        _userQuery.Setup(q => q.GetById(1, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await _sut.ConfirmEmailManually(new ConfirmUserEmailRequest { Id = 1 }, CancellationToken.None);

        result.IsEmailConfirmed.Should().BeTrue();
        _userCommand.Verify(c => c.Update(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmEmailManually_WhenNotConfirmed_UpdatesUser()
    {
        var user = new User
        {
            Id = 1,
            Email = "a@b.com",
            IsEmailConfirmed = false,
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow,
        };
        _userQuery.Setup(q => q.GetById(1, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _userCommand.Setup(c => c.Update(It.Is<User>(u => u.IsEmailConfirmed), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _sut.ConfirmEmailManually(new ConfirmUserEmailRequest { Id = 1 }, CancellationToken.None);

        result.IsEmailConfirmed.Should().BeTrue();
        _userCommand.VerifyAll();
    }

    [Fact]
    public async Task GetOrCreateDashboardUserForTelegramAsync_WhenTelegramBotUserMissing_ReturnsNull()
    {
        _telegramBotUserQuery.Setup(q => q.GetByTelegramId(123, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TelegramBotUser?)null);

        var result = await _sut.GetOrCreateDashboardUserForTelegramAsync(123, CancellationToken.None);

        result.Should().BeNull();
        _userIdentityLinkQuery.Verify(
            q => q.GetByProviderAndExternalId(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetOrCreateDashboardUserForTelegramAsync_WhenLinkExists_ReturnsExistingUser()
    {
        var tgUser = new TelegramBotUser { Id = 5, TelegramId = 123, Username = "tg", CreateDate = DateTimeOffset.UtcNow, LastUpdate = DateTimeOffset.UtcNow };
        var link = new UserIdentityLink { Id = 1, UserId = 10, Provider = "telegram", ExternalId = "123" };
        var user = new User { Id = 10, DisplayName = "existing", CreateDate = DateTimeOffset.UtcNow, LastUpdate = DateTimeOffset.UtcNow };

        _telegramBotUserQuery.Setup(q => q.GetByTelegramId(123, It.IsAny<CancellationToken>())).ReturnsAsync(tgUser);
        _userIdentityLinkQuery.Setup(q => q.GetByProviderAndExternalId("telegram", "123", It.IsAny<CancellationToken>())).ReturnsAsync(link);
        _userQuery.Setup(q => q.GetById(10, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await _sut.GetOrCreateDashboardUserForTelegramAsync(123, CancellationToken.None);

        result.Should().BeSameAs(user);
        _userCommand.Verify(c => c.Add(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOrCreateDashboardUserForTelegramAsync_WhenNoLink_CreatesUserLinkAndQuota()
    {
        var ct = CancellationToken.None;
        var now = DateTimeOffset.UtcNow;
        var tgUser = new TelegramBotUser { Id = 5, TelegramId = 456, Username = "newtg", CreateDate = now, LastUpdate = now };
        var defaultPlan = new QuotaPlan { Id = 2, Name = "Default", CreateDate = now, LastUpdate = now };

        _telegramBotUserQuery.Setup(q => q.GetByTelegramId(456, ct)).ReturnsAsync(tgUser);
        _userIdentityLinkQuery.Setup(q => q.GetByProviderAndExternalId("telegram", "456", ct)).ReturnsAsync((UserIdentityLink?)null);
        _userCommand.Setup(c => c.Add(It.IsAny<User>(), true, ct))
            .ReturnsAsync((User u, bool _, CancellationToken _) => new User { Id = 77, DisplayName = u.DisplayName, CreateDate = u.CreateDate, LastUpdate = u.LastUpdate });
        _userIdentityLinkCommand.Setup(c => c.Add(It.IsAny<UserIdentityLink>(), true, ct))
            .ReturnsAsync((UserIdentityLink l, bool _, CancellationToken _) => l);
        _quotaPlanQuery.Setup(q => q.GetDefault(ct)).ReturnsAsync(defaultPlan);
        _userQuotaPlanCommand.Setup(c => c.Add(It.IsAny<UserQuotaPlan>(), true, ct))
            .ReturnsAsync((UserQuotaPlan p, bool _, CancellationToken _) => p);

        var result = await _sut.GetOrCreateDashboardUserForTelegramAsync(456, ct);

        result.Should().NotBeNull();
        result!.Id.Should().Be(77);
        result.DisplayName.Should().Be("newtg");
        _userIdentityLinkCommand.Verify(c => c.Add(It.Is<UserIdentityLink>(l => l.ExternalId == "456"), true, ct), Times.Once);
        _userQuotaPlanCommand.Verify(c => c.Add(It.IsAny<UserQuotaPlan>(), true, ct), Times.Once);
    }

    [Fact]
    public async Task RegisterUserFromTgBot_WhenNotificationFails_StillReturnsResponse()
    {
        var ct = CancellationToken.None;
        _telegramBotUserQuery.Setup(q => q.GetByTelegramId(111, ct)).ReturnsAsync((TelegramBotUser?)null);
        _telegramBotUserCommand.Setup(c => c.Add(It.IsAny<TelegramBotUser>(), true, ct))
            .ReturnsAsync((TelegramBotUser e, bool _, CancellationToken _) =>
                new TelegramBotUser { Id = 1, TelegramId = e.TelegramId, Username = e.Username, CreateDate = DateTimeOffset.UtcNow, LastUpdate = DateTimeOffset.UtcNow });
        _userIdentityLinkQuery.Setup(q => q.GetByProviderAndExternalId("telegram", "111", ct)).ReturnsAsync((UserIdentityLink?)null);
        _userCommand.Setup(c => c.Add(It.IsAny<User>(), true, ct))
            .ReturnsAsync((User u, bool _, CancellationToken _) => new User { Id = 50, DisplayName = u.DisplayName ?? "x", CreateDate = u.CreateDate, LastUpdate = u.LastUpdate });
        _userIdentityLinkCommand.Setup(c => c.Add(It.IsAny<UserIdentityLink>(), true, ct))
            .ReturnsAsync((UserIdentityLink l, bool _, CancellationToken _) => l);
        _quotaPlanQuery.Setup(q => q.GetDefault(ct)).ReturnsAsync((QuotaPlan?)null);
        _userIdentityLinkQuery.Setup(q => q.GetByUserId(50, ct)).ReturnsAsync((UserIdentityLink?)null);
        _appNotificationFacade.Setup(f => f.UserRegistered(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), ct))
            .ThrowsAsync(new InvalidOperationException("notify failed"));

        var result = await _sut.RegisterUserFromTgBot(new RegisterUserFromTgBotRequest { TelegramId = 111, Username = "x" }, ct);

        result.User!.Id.Should().Be(50);
    }
}
