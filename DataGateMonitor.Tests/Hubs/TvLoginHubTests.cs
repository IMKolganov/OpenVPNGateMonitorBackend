using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Moq;
using DataGateMonitor.DataBase.Services.Query.TvLoginSessionTable;
using DataGateMonitor.Hubs;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Responses;
using Xunit;

namespace DataGateMonitor.Tests.Hubs;

public class TvLoginHubTests
{
    private sealed class TestHubCallerContext : HubCallerContext
    {
        private readonly string _connectionId;
        private readonly CancellationToken _aborted;
        private readonly IDictionary<object, object?> _items = new Dictionary<object, object?>();

        public TestHubCallerContext(string connectionId, CancellationToken aborted = default)
        {
            _connectionId = connectionId;
            _aborted = aborted;
        }

        public override string ConnectionId => _connectionId;
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User => null;
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override IDictionary<object, object?> Items => _items;
        public override CancellationToken ConnectionAborted => _aborted;
        public override void Abort() { }
    }

    [Fact]
    public void Hub_IsAllowAnonymous_And_HasStablePathConstants()
    {
        Assert.NotNull(typeof(TvLoginHub).GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal("/api/hubs/tv-login", TvLoginHub.HubPath);
        Assert.Equal("TvLoginSessionStatusChanged", TvLoginHub.StatusChangedEvent);

        var sessionId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Assert.Equal("tv-login:11111111-2222-3333-4444-555555555555", TvLoginHub.GroupName(sessionId));
    }

    [Theory]
    [InlineData(TvLoginSessionStatus.Pending, "pending")]
    [InlineData(TvLoginSessionStatus.Viewed, "viewed")]
    [InlineData(TvLoginSessionStatus.Approved, "approved")]
    [InlineData(TvLoginSessionStatus.Denied, "denied")]
    [InlineData(TvLoginSessionStatus.Expired, "expired")]
    [InlineData(TvLoginSessionStatus.Consumed, "consumed")]
    public async Task WatchSession_SendsImmediateSnapshot_ForEachStatus(
        TvLoginSessionStatus status,
        string expectedStatus)
    {
        var sessionId = Guid.NewGuid();
        var expires = DateTimeOffset.UtcNow.AddMinutes(4);
        var query = new Mock<ITvLoginSessionQueryService>();
        query.Setup(q => q.GetById(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TvLoginSession
            {
                Id = sessionId,
                UserCode = "123456",
                Status = status,
                ExpiresAt = expires,
            });

        var groups = new Mock<IGroupManager>();
        groups.Setup(g => g.AddToGroupAsync(
                "conn-1",
                TvLoginHub.GroupName(sessionId),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        TvLoginSessionStatusEvent? pushed = null;
        var caller = new Mock<ISingleClientProxy>();
        caller.Setup(c => c.SendCoreAsync(
                TvLoginHub.StatusChangedEvent,
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((_, args, _) =>
            {
                pushed = Assert.IsType<TvLoginSessionStatusEvent>(args[0]);
            })
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubCallerClients>();
        clients.SetupGet(c => c.Caller).Returns(caller.Object);

        var hub = new TvLoginHub(query.Object)
        {
            Context = new TestHubCallerContext("conn-1"),
            Groups = groups.Object,
            Clients = clients.Object,
        };

        await hub.WatchSession(sessionId);

        groups.Verify();
        Assert.NotNull(pushed);
        Assert.Equal(sessionId, pushed!.SessionId);
        Assert.Equal(expectedStatus, pushed.Status);
        Assert.Equal(expires, pushed.ExpiresAt);
    }

    [Fact]
    public async Task WatchSession_WhenPendingButPastExpiry_SnapshotSaysExpired()
    {
        var sessionId = Guid.NewGuid();
        var query = new Mock<ITvLoginSessionQueryService>();
        query.Setup(q => q.GetById(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TvLoginSession
            {
                Id = sessionId,
                UserCode = "123456",
                Status = TvLoginSessionStatus.Pending,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            });

        TvLoginSessionStatusEvent? pushed = null;
        var caller = new Mock<ISingleClientProxy>();
        caller.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((_, args, _) =>
                pushed = Assert.IsType<TvLoginSessionStatusEvent>(args[0]))
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubCallerClients>();
        clients.SetupGet(c => c.Caller).Returns(caller.Object);

        var hub = new TvLoginHub(query.Object)
        {
            Context = new TestHubCallerContext("conn-exp"),
            Groups = Mock.Of<IGroupManager>(),
            Clients = clients.Object,
        };

        await hub.WatchSession(sessionId);

        Assert.Equal("expired", pushed!.Status);
    }

    [Fact]
    public async Task WatchSession_WhenViewedButPastExpiry_SnapshotSaysExpired()
    {
        var sessionId = Guid.NewGuid();
        var query = new Mock<ITvLoginSessionQueryService>();
        query.Setup(q => q.GetById(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TvLoginSession
            {
                Id = sessionId,
                UserCode = "123456",
                Status = TvLoginSessionStatus.Viewed,
                ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-30),
            });

        TvLoginSessionStatusEvent? pushed = null;
        var caller = new Mock<ISingleClientProxy>();
        caller.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((_, args, _) =>
                pushed = Assert.IsType<TvLoginSessionStatusEvent>(args[0]))
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubCallerClients>();
        clients.SetupGet(c => c.Caller).Returns(caller.Object);

        var hub = new TvLoginHub(query.Object)
        {
            Context = new TestHubCallerContext("conn-viewed-exp"),
            Groups = Mock.Of<IGroupManager>(),
            Clients = clients.Object,
        };

        await hub.WatchSession(sessionId);

        Assert.Equal("expired", pushed!.Status);
    }

    [Fact]
    public async Task WatchSession_UnknownStatus_SnapshotSaysExpired()
    {
        var sessionId = Guid.NewGuid();
        var session = new TvLoginSession
        {
            Id = sessionId,
            UserCode = "123456",
            Status = (TvLoginSessionStatus)999,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
        };
        var query = new Mock<ITvLoginSessionQueryService>();
        query.Setup(q => q.GetById(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);

        TvLoginSessionStatusEvent? pushed = null;
        var caller = new Mock<ISingleClientProxy>();
        caller.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((_, args, _) =>
                pushed = Assert.IsType<TvLoginSessionStatusEvent>(args[0]))
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubCallerClients>();
        clients.SetupGet(c => c.Caller).Returns(caller.Object);

        var hub = new TvLoginHub(query.Object)
        {
            Context = new TestHubCallerContext("conn-unknown"),
            Groups = Mock.Of<IGroupManager>(),
            Clients = clients.Object,
        };

        await hub.WatchSession(sessionId);

        Assert.Equal("expired", pushed!.Status);
        Assert.Equal((TvLoginSessionStatus)999, session.Status);
    }

    [Fact]
    public async Task WatchSession_WhenPendingPastExpiry_DoesNotMutatePersistedStatus()
    {
        var sessionId = Guid.NewGuid();
        var session = new TvLoginSession
        {
            Id = sessionId,
            UserCode = "123456",
            Status = TvLoginSessionStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        };
        var query = new Mock<ITvLoginSessionQueryService>();
        query.Setup(q => q.GetById(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var caller = new Mock<ISingleClientProxy>();
        caller.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var clients = new Mock<IHubCallerClients>();
        clients.SetupGet(c => c.Caller).Returns(caller.Object);

        var hub = new TvLoginHub(query.Object)
        {
            Context = new TestHubCallerContext("conn-no-write"),
            Groups = Mock.Of<IGroupManager>(),
            Clients = clients.Object,
        };

        await hub.WatchSession(sessionId);

        Assert.Equal(TvLoginSessionStatus.Pending, session.Status);
    }

    [Fact]
    public async Task WatchSession_WhenMissing_ThrowsHubException()
    {
        var sessionId = Guid.NewGuid();
        var query = new Mock<ITvLoginSessionQueryService>();
        query.Setup(q => q.GetById(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TvLoginSession?)null);

        var hub = new TvLoginHub(query.Object)
        {
            Context = new TestHubCallerContext("conn-missing"),
            Groups = Mock.Of<IGroupManager>(),
            Clients = Mock.Of<IHubCallerClients>(),
        };

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.WatchSession(sessionId));
        Assert.Equal("TV login session not found.", ex.Message);
    }

    [Fact]
    public async Task UnwatchSession_RemovesFromGroup()
    {
        var sessionId = Guid.NewGuid();
        var groups = new Mock<IGroupManager>();
        groups.Setup(g => g.RemoveFromGroupAsync(
                "conn-2",
                TvLoginHub.GroupName(sessionId),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var hub = new TvLoginHub(Mock.Of<ITvLoginSessionQueryService>())
        {
            Context = new TestHubCallerContext("conn-2"),
            Groups = groups.Object,
        };

        await hub.UnwatchSession(sessionId);

        groups.Verify();
    }
}
