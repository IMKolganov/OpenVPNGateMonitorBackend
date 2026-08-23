using DataGateMonitor.Hubs;
using DataGateMonitor.Services.DataGateOpenVpnManager.Events;
using DataGateMonitor.Services.DataGateOpenVpnManager.OpenVpnProxy.Hubs;
using DataGateMonitor.Services.DataGateOpenVpnManager.OpenVpnProxy.Hubs.Interfaces;
using DataGateMonitor.Services.Others.Notifications.OpenVpnMicroserviceClient;
using DataGateMonitor.Tests.Helpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DataGateMonitor.Tests.Services.DataGateOpenVpnManager.Events;

public class OpenVpnEventClientStartRetryTests
{
    [Fact]
    public async Task StartListeningAsync_OnStartFailure_RetriesWithInjectableDelay_AndNotifiesOnce()
    {
        var server = OpenVpnHubTestHelpers.OpenVpnServer();
        var failFirstStart = true;
        var proxy = new FakeHubConnectionProxy();
        proxy.StartAsyncOverride = _ =>
        {
            if (failFirstStart)
            {
                failFirstStart = false;
                throw new InvalidOperationException("hub unreachable");
            }

            proxy.State = HubConnectionState.Connected;
            return Task.CompletedTask;
        };

        var delayCalls = 0;
        var notifications = new Mock<IOpenVpnMicroserviceNotificationService>();
        notifications
            .Setup(x => x.NotifyEventHubConnectionFailed(
                server.Id, server.ServerName, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var scopeFactory = OpenVpnHubTestHelpers.CreateScopeFactory(notifications.Object);

        var client = new OpenVpnEventClient(
            server,
            NullLogger<OpenVpnEventClient>.Instance,
            Mock.Of<IHubContext<OpenVpnEventHub>>(),
            OpenVpnHubTestHelpers.CreateTokenService(),
            scopeFactory,
            new SingleProxyEventHubConnectionFactory(proxy),
            startRetryDelay: TimeSpan.FromMilliseconds(5),
            retryDelayAsync: (delay, ct) =>
            {
                delayCalls++;
                Assert.Equal(TimeSpan.FromMilliseconds(5), delay);
                return Task.CompletedTask;
            });

        await client.StartListeningAsync(CancellationToken.None);

        Assert.Equal(2, proxy.StartCallCount);
        Assert.Equal(1, delayCalls);
        Assert.Equal(HubConnectionState.Connected, proxy.State);
        notifications.Verify(
            x => x.NotifyEventHubConnectionFailed(
                server.Id, server.ServerName, It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StopAsync_DuringRetryBackoff_CancelsStartListening()
    {
        var server = OpenVpnHubTestHelpers.OpenVpnServer();
        var proxy = new FakeHubConnectionProxy();
        proxy.StartAsyncOverride = _ => throw new InvalidOperationException("hub unreachable");

        var delayEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDelay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var client = new OpenVpnEventClient(
            server,
            NullLogger<OpenVpnEventClient>.Instance,
            Mock.Of<IHubContext<OpenVpnEventHub>>(),
            OpenVpnHubTestHelpers.CreateTokenService(),
            OpenVpnHubTestHelpers.CreateScopeFactory(Mock.Of<IOpenVpnMicroserviceNotificationService>()),
            new SingleProxyEventHubConnectionFactory(proxy),
            startRetryDelay: TimeSpan.FromSeconds(30),
            retryDelayAsync: async (_, ct) =>
            {
                delayEntered.TrySetResult();
                await using var reg = ct.Register(() => releaseDelay.TrySetResult());
                await releaseDelay.Task.WaitAsync(ct);
            });

        var startTask = client.StartListeningAsync(CancellationToken.None);
        await delayEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await client.StopAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => startTask);
        Assert.Equal("Disconnected", client.GetStatus().ConnectionStatus.State);
    }

    /// <summary>
    /// Mis-registered OpenVPN ApiUrl pointing at Xray :9443 keeps failing JWT audience;
    /// DeleteVpnServer → StopAsync must cancel the retry loop (not leave zombie negotiate).
    /// </summary>
    [Fact]
    public async Task StopAsync_DuringRetry_ToWrongXrayApiUrl_StopsAudienceFailures()
    {
        var server = OpenVpnHubTestHelpers.OpenVpnServer(
            id: 91,
            apiUrl: "https://xs1-pol.datagateapp.com:9443/");
        var proxy = new FakeHubConnectionProxy();
        proxy.StartAsyncOverride = _ =>
            throw new InvalidOperationException("IDX10214: Audience validation failed");

        var delayEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new OpenVpnEventClient(
            server,
            NullLogger<OpenVpnEventClient>.Instance,
            Mock.Of<IHubContext<OpenVpnEventHub>>(),
            OpenVpnHubTestHelpers.CreateTokenService(),
            OpenVpnHubTestHelpers.CreateScopeFactory(Mock.Of<IOpenVpnMicroserviceNotificationService>()),
            new SingleProxyEventHubConnectionFactory(proxy),
            startRetryDelay: TimeSpan.FromSeconds(30),
            retryDelayAsync: async (_, ct) =>
            {
                delayEntered.TrySetResult();
                await Task.Delay(Timeout.Infinite, ct);
            });

        var startTask = client.StartListeningAsync(CancellationToken.None);
        await delayEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Same path as OpenVpnEventClientFactory.Remove / DeleteVpnServer
        await client.StopAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => startTask);
        Assert.True(proxy.StartCallCount >= 1);
        Assert.Equal("Disconnected", client.GetStatus().ConnectionStatus.State);
    }
}
