using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using DataGateMonitor.Hubs;
using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Responses;
using Xunit;

namespace DataGateMonitor.Tests.Hubs;

public class TvLoginHubNotifierTests
{
    [Fact]
    public async Task NotifyStatusAsync_SendsEventToSessionGroup()
    {
        var sessionId = Guid.NewGuid();
        var expires = DateTimeOffset.UtcNow.AddMinutes(2);

        var hubContext = new Mock<IHubContext<TvLoginHub>>();
        var clients = new Mock<IHubClients>();
        var group = new Mock<IClientProxy>();
        var logger = new Mock<ILogger<TvLoginHubNotifier>>();

        hubContext.SetupGet(h => h.Clients).Returns(clients.Object);
        clients.Setup(c => c.Group(TvLoginHub.GroupName(sessionId))).Returns(group.Object);

        TvLoginSessionStatusEvent? payload = null;
        group.Setup(c => c.SendCoreAsync(
                TvLoginHub.StatusChangedEvent,
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((_, args, _) =>
                payload = Assert.IsType<TvLoginSessionStatusEvent>(args[0]))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var sut = new TvLoginHubNotifier(hubContext.Object, logger.Object);

        await sut.NotifyStatusAsync(sessionId, "viewed", expires, CancellationToken.None);

        group.Verify();
        Assert.NotNull(payload);
        Assert.Equal(sessionId, payload!.SessionId);
        Assert.Equal("viewed", payload.Status);
        Assert.Equal(expires, payload.ExpiresAt);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("viewed")]
    [InlineData("approved")]
    [InlineData("denied")]
    [InlineData("expired")]
    [InlineData("consumed")]
    public async Task NotifyStatusAsync_AcceptsAllLifecycleStatuses(string status)
    {
        var sessionId = Guid.NewGuid();
        var hubContext = new Mock<IHubContext<TvLoginHub>>();
        var clients = new Mock<IHubClients>();
        var group = new Mock<IClientProxy>();
        hubContext.SetupGet(h => h.Clients).Returns(clients.Object);
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(group.Object);
        group.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new TvLoginHubNotifier(hubContext.Object, Mock.Of<ILogger<TvLoginHubNotifier>>());

        await sut.NotifyStatusAsync(sessionId, status, DateTimeOffset.UtcNow, CancellationToken.None);

        group.Verify(
            c => c.SendCoreAsync(
                TvLoginHub.StatusChangedEvent,
                It.Is<object?[]>(args =>
                    args.Length > 0
                    && args[0] is TvLoginSessionStatusEvent
                    && ((TvLoginSessionStatusEvent)args[0]!).Status == status),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NotifyStatusAsync_WhenSendFails_LogsWarning_AndDoesNotThrow()
    {
        var sessionId = Guid.NewGuid();
        var hubContext = new Mock<IHubContext<TvLoginHub>>();
        var clients = new Mock<IHubClients>();
        var group = new Mock<IClientProxy>();
        var logger = new Mock<ILogger<TvLoginHubNotifier>>();

        hubContext.SetupGet(h => h.Clients).Returns(clients.Object);
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(group.Object);

        var boom = new InvalidOperationException("signalr down");
        group.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(boom);

        var sut = new TvLoginHubNotifier(hubContext.Object, logger.Object);

        await sut.NotifyStatusAsync(sessionId, "approved", DateTimeOffset.UtcNow, CancellationToken.None);

        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state!.ToString()!.Contains("Failed to push TV login status")
                    && state!.ToString()!.Contains("approved")),
                boom,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
