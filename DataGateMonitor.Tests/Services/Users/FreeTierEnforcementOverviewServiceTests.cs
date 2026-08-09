using DataGateMonitor.DataBase.Services.Query.IssuedOvpnFileTable;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;
using DataGateMonitor.DataBase.Services.Query.UserQuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerClientTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Models;
using DataGateMonitor.Models.Helpers;
using DataGateMonitor.Services.Users;
using DataGateMonitor.Services.Users.Interfaces;
using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Requests;
using DataGateMonitor.SharedModels.Responses;
using DataGateMonitor.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;

namespace DataGateMonitor.Tests.Services.Users;

public class FreeTierEnforcementOverviewServiceTests
{
    private readonly Mock<IUserQuotaPlanQueryService> _userQuotaPlanQueryService = new();
    private readonly Mock<IQuotaPlanQueryService> _quotaPlanQueryService = new();
    private readonly Mock<IUserQueryService> _userQueryService = new();
    private readonly Mock<IUserIdentityLinkQueryService> _userIdentityLinkQueryService = new();
    private readonly Mock<IIssuedOvpnFileQueryService> _issuedOvpnFileQueryService = new();
    private readonly Mock<IVpnServerClientQueryService> _vpnServerClientQueryService = new();
    private readonly Mock<IVpnServerQueryService> _vpnServerQueryService = new();
    private readonly Mock<IFreeTierAccessComplianceService> _complianceService = new();
    private readonly Mock<IQueryService<FreeTierDisconnectLog, int>> _disconnectLogQueryService = new();

    private FreeTierEnforcementOverviewService CreateSut()
        => new(
            _userQuotaPlanQueryService.Object,
            _quotaPlanQueryService.Object,
            _userQueryService.Object,
            _userIdentityLinkQueryService.Object,
            _issuedOvpnFileQueryService.Object,
            _vpnServerClientQueryService.Object,
            _vpnServerQueryService.Object,
            _complianceService.Object,
            _disconnectLogQueryService.Object,
            Mock.Of<ILogger<FreeTierEnforcementOverviewService>>());

    private void SetupCommonPlans()
    {
        _quotaPlanQueryService.Setup(x => x.GetAll(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new QuotaPlan { Id = 1, Name = QuotaPlanNames.Free },
            new QuotaPlan { Id = 2, Name = QuotaPlanNames.Default },
            new QuotaPlan { Id = 3, Name = "Pro" },
        ]);
        _vpnServerQueryService.Setup(x => x.GetAll(false, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new VpnServer { Id = 100, ServerName = "srv-100" }]);
        _vpnServerClientQueryService.Setup(x => x.GetAllConnected(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    [Fact]
    public async Task GetCandidatesAsync_ExcludesUsersOnPaidPlans()
    {
        SetupCommonPlans();
        _userQuotaPlanQueryService.Setup(x => x.GetAllActive(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new UserQuotaPlan { UserId = 1, QuotaPlanId = 3 },
        ]);

        var sut = CreateSut();
        var result = await sut.GetCandidatesAsync(CancellationToken.None);

        Assert.Empty(result.Candidates);
        _complianceService.Verify(
            x => x.EvaluateAccessForEnforcementAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetCandidatesAsync_ExcludesCompliantFreeUsers()
    {
        SetupCommonPlans();
        _userQuotaPlanQueryService.Setup(x => x.GetAllActive(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new UserQuotaPlan { UserId = 1, QuotaPlanId = 1 },
        ]);
        _complianceService
            .Setup(x => x.EvaluateAccessForEnforcementAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FreeTierAccessComplianceResult { IsApplicable = true, IsCompliant = true });

        var sut = CreateSut();
        var result = await sut.GetCandidatesAsync(CancellationToken.None);

        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task GetCandidatesAsync_IncludesNonCompliantFreeUser_AndMarksConnectedSession()
    {
        SetupCommonPlans();
        _userQuotaPlanQueryService.Setup(x => x.GetAllActive(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new UserQuotaPlan { UserId = 42, QuotaPlanId = 1 },
        ]);
        _complianceService
            .Setup(x => x.EvaluateAccessForEnforcementAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FreeTierAccessComplianceResult
            {
                IsApplicable = true,
                IsCompliant = false,
                ActivePlanName = QuotaPlanNames.Free,
                TelegramId = 555,
            });
        _userQueryService
            .Setup(x => x.GetById(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 42, DisplayName = "Bob", Email = "bob@example.com" });
        _userIdentityLinkQueryService
            .Setup(x => x.GetListByUserId(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new UserIdentityLink { UserId = 42, Provider = "telegram", ExternalId = "555" }]);
        _issuedOvpnFileQueryService
            .Setup(x => x.GetAllByExternalId("555", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new IssuedOvpnFile { Id = 1, VpnServerId = 100, CommonName = "cn-42", ExternalId = "555" }]);

        var connectedSince = DateTimeOffset.UtcNow.AddMinutes(-10);
        _vpnServerClientQueryService.Setup(x => x.GetAllConnected(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new VpnServerClient { VpnServerId = 100, CommonName = "cn-42", IsConnected = true, ConnectedSince = connectedSince },
        ]);

        var sut = CreateSut();
        var result = await sut.GetCandidatesAsync(CancellationToken.None);

        Assert.Single(result.Candidates);
        var candidate = result.Candidates[0];
        Assert.Equal(42, candidate.UserId);
        Assert.Equal("Bob", candidate.DisplayName);
        Assert.True(candidate.IsConnected);
        Assert.Equal(100, candidate.VpnServerId);
        Assert.Equal("cn-42", candidate.CommonName);
        Assert.Equal("srv-100", candidate.VpnServerName);
        Assert.Equal(connectedSince, candidate.ConnectedSince);
        Assert.Equal(1, result.ConnectedCount);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetUnsubscribedConnectedAsync_IncludesGraceUserWhoIsOnlineButNotSubscribed()
    {
        SetupCommonPlans();
        _userQuotaPlanQueryService.Setup(x => x.GetAllActive(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new UserQuotaPlan { UserId = 42, QuotaPlanId = 1 },
        ]);
        // Grace: compliant for disconnect, but still not subscribed.
        _complianceService
            .Setup(x => x.EvaluateAccessForEnforcementAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FreeTierAccessComplianceResult
            {
                IsApplicable = true,
                IsCompliant = true,
                IsGracePeriod = true,
                IsChannelSubscribed = false,
                ActivePlanName = QuotaPlanNames.Free,
                TelegramId = 555,
            });
        _userQueryService
            .Setup(x => x.GetById(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 42, DisplayName = "Grace" });
        _userIdentityLinkQueryService
            .Setup(x => x.GetListByUserId(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new UserIdentityLink { UserId = 42, Provider = "telegram", ExternalId = "555" }]);
        _issuedOvpnFileQueryService
            .Setup(x => x.GetAllByExternalId("555", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new IssuedOvpnFile { Id = 1, VpnServerId = 100, CommonName = "cn-42", ExternalId = "555" }]);
        _vpnServerClientQueryService.Setup(x => x.GetAllConnected(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new VpnServerClient { VpnServerId = 100, CommonName = "cn-42", IsConnected = true },
        ]);

        var sut = CreateSut();
        var digest = await sut.GetUnsubscribedConnectedAsync(CancellationToken.None);
        var disconnectCandidates = await sut.GetCandidatesAsync(CancellationToken.None);

        Assert.Single(digest.Candidates);
        Assert.Equal(42, digest.Candidates[0].UserId);
        Assert.Empty(disconnectCandidates.Candidates);
    }

    [Fact]
    public async Task GetUnsubscribedConnectedAsync_ExcludesOfflineUnsubscribedUsers()
    {
        SetupCommonPlans();
        _userQuotaPlanQueryService.Setup(x => x.GetAllActive(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new UserQuotaPlan { UserId = 42, QuotaPlanId = 1 },
        ]);
        _complianceService
            .Setup(x => x.EvaluateAccessForEnforcementAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FreeTierAccessComplianceResult
            {
                IsApplicable = true,
                IsCompliant = false,
                IsChannelSubscribed = false,
                TelegramId = 555,
            });
        _userQueryService
            .Setup(x => x.GetById(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 42, DisplayName = "Offline" });
        _userIdentityLinkQueryService
            .Setup(x => x.GetListByUserId(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new UserIdentityLink { UserId = 42, Provider = "telegram", ExternalId = "555" }]);
        _issuedOvpnFileQueryService
            .Setup(x => x.GetAllByExternalId("555", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new IssuedOvpnFile { Id = 1, VpnServerId = 100, CommonName = "cn-42", ExternalId = "555" }]);

        var sut = CreateSut();
        var digest = await sut.GetUnsubscribedConnectedAsync(CancellationToken.None);

        Assert.Empty(digest.Candidates);
    }

    [Fact]
    public async Task GetDisconnectLogAsync_MapsPagedResultToDto()
    {
        var now = DateTimeOffset.UtcNow;
        var page = new TestPagedResult<FreeTierDisconnectLog>
        {
            Page = 1,
            PageSize = 20,
            TotalCount = 1,
            Items =
            [
                new FreeTierDisconnectLog
                {
                    Id = 9,
                    UserId = 1,
                    VpnServerId = 100,
                    CommonName = "cn-1",
                    Reason = 0,
                    KillSucceeded = true,
                    CreatedAt = now,
                },
            ],
        };
        _disconnectLogQueryService
            .Setup(x => x.Page(
                1, 20,
                It.IsAny<System.Linq.Expressions.Expression<Func<FreeTierDisconnectLog, bool>>>(),
                It.IsAny<Func<IQueryable<FreeTierDisconnectLog>, IOrderedQueryable<FreeTierDisconnectLog>>>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        var sut = CreateSut();
        var result = await sut.GetDisconnectLogAsync(
            new GetFreeTierDisconnectLogRequest { Page = 1, PageSize = 20 }, CancellationToken.None);

        Assert.Equal(1, result.Entries.TotalCount);
        Assert.Single(result.Entries.Items);
        Assert.Equal(9, result.Entries.Items[0].Id);
        Assert.Equal("cn-1", result.Entries.Items[0].CommonName);
    }
}
