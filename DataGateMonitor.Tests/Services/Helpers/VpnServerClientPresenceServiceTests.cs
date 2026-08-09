using System.Linq.Expressions;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.Services.Helpers;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using Moq;

namespace DataGateMonitor.Tests.Services.Helpers;

public class VpnServerClientPresenceServiceTests
{
    private readonly Mock<ICommandService<VpnServerClient, int>> _clientCmd = new();
    private readonly Mock<IConnectedClientsCounterStore> _counter = new();
    private readonly Mock<ILogger<VpnServerClientPresenceService>> _logger = new();

    private VpnServerClientPresenceService CreateSut() => new(
        _clientCmd.Object,
        _counter.Object,
        _logger.Object);

    [Fact]
    public async Task MarkAllDisconnectedAsync_UpdatesConnectedSessions_AndZerosRedis()
    {
        Expression<Func<VpnServerClient, bool>>? capturedPredicate = null;
        _clientCmd
            .Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServerClient, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServerClient>>>(),
                It.IsAny<CancellationToken>()))
            .Callback<Expression<Func<VpnServerClient, bool>>, Action<UpdateSettersBuilder<VpnServerClient>>, CancellationToken>(
                (pred, _, _) => capturedPredicate = pred)
            .ReturnsAsync(3);
        _counter.Setup(c => c.SetAsync(42, 0, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().MarkAllDisconnectedAsync(42, CancellationToken.None);

        Assert.NotNull(capturedPredicate);
        var compiled = capturedPredicate!.Compile();
        Assert.True(compiled(new VpnServerClient { VpnServerId = 42, IsConnected = true }));
        Assert.False(compiled(new VpnServerClient { VpnServerId = 42, IsConnected = false }));
        Assert.False(compiled(new VpnServerClient { VpnServerId = 99, IsConnected = true }));
        _counter.Verify(c => c.SetAsync(42, 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkAllDisconnectedAsync_WhenNoConnectedRows_StillZerosRedis()
    {
        _clientCmd
            .Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServerClient, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServerClient>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _counter.Setup(c => c.SetAsync(55, 0, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().MarkAllDisconnectedAsync(55, CancellationToken.None);

        _counter.Verify(c => c.SetAsync(55, 0, It.IsAny<CancellationToken>()), Times.Once);
    }
}
