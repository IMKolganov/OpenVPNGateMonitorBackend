using System.Net;
using System.Text;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.SharedModels.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DataGateMonitor.Tests.Services.Api;

public class VpnServerOpenVpnProcessServiceTests
{
    private readonly Mock<IVpnServerQueryService> _query = new();
    private readonly Mock<IMicroserviceTokenService> _tokens = new();
    private readonly QueueHandler _handler = new();

    private VpnServerOpenVpnProcessService CreateSut()
    {
        _tokens.Setup(t => t.GenerateToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test-jwt");

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(_handler) { BaseAddress = new Uri("http://unused/") });

        return new VpnServerOpenVpnProcessService(
            _query.Object,
            factory.Object,
            _tokens.Object,
            NullLogger<VpnServerOpenVpnProcessService>.Instance);
    }

    private void SetupOpenVpnServer(int id = 7, string apiUrl = "https://node.example/")
    {
        _query.Setup(q => q.GetById(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServer
            {
                Id = id,
                ServerName = "node",
                ServerType = VpnServerType.OpenVpn,
                ApiUrl = apiUrl
            });
    }

    private static HttpResponseMessage OkProcess(string action, bool running, int? pid = 42) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent($$"""
                {
                  "success": true,
                  "data": {
                    "action": "{{action}}",
                    "isRunning": {{(running ? "true" : "false")}},
                    "pid": {{(pid.HasValue ? pid.Value.ToString() : "null")}},
                    "configPath": "/mnt/server.conf",
                    "pidFilePath": "/mnt/openvpn.pid",
                    "message": "{{action}} ok"
                  }
                }
                """, Encoding.UTF8, "application/json")
        };

    [Fact]
    public async Task GetStatusAsync_WhenServerMissing_Throws()
    {
        _query.Setup(q => q.GetById(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServer?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSut().GetStatusAsync(9, CancellationToken.None));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartAsync_WhenNotOpenVpn_Throws()
    {
        _query.Setup(q => q.GetById(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServer
            {
                Id = 3,
                ServerType = VpnServerType.Xray,
                ApiUrl = "https://x.example/"
            });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSut().StartAsync(3, CancellationToken.None));
        Assert.Contains("OpenVPN", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetStatusAsync_WhenApiUrlMissing_Throws()
    {
        SetupOpenVpnServer(apiUrl: "  ");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSut().GetStatusAsync(7, CancellationToken.None));
        Assert.Contains("API URL", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetStatusAsync_CallsManagerStatus_WithBearerAndUnwraps()
    {
        SetupOpenVpnServer();
        _handler.Enqueue(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Equal("https://node.example/api/openvpn/status", req.RequestUri!.ToString());
            Assert.Equal("Bearer", req.Headers.Authorization!.Scheme);
            Assert.Equal("test-jwt", req.Headers.Authorization.Parameter);
            return OkProcess("status", running: true, pid: 99);
        });

        var result = await CreateSut().GetStatusAsync(7, CancellationToken.None);

        Assert.Equal("status", result.Action);
        Assert.True(result.IsRunning);
        Assert.Equal(99, result.Pid);
        Assert.Equal("status ok", result.Message);
        _tokens.Verify(t => t.GenerateToken("vpn-cert-issuer", "cert-create", "backend", "DataGateOpenVpnManager"),
            Times.Once);
    }

    [Theory]
    [InlineData("start", "api/openvpn/start")]
    [InlineData("restart", "api/openvpn/restart")]
    [InlineData("kill", "api/openvpn/kill")]
    public async Task ProcessActions_PostExpectedPaths(string action, string relativePath)
    {
        SetupOpenVpnServer();
        _handler.Enqueue(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.EndsWith(relativePath, req.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
            return OkProcess(action, running: action != "kill", pid: action == "kill" ? null : 7);
        });

        var sut = CreateSut();
        var result = action switch
        {
            "start" => await sut.StartAsync(7, CancellationToken.None),
            "restart" => await sut.RestartAsync(7, CancellationToken.None),
            "kill" => await sut.KillAsync(7, CancellationToken.None),
            _ => throw new InvalidOperationException(action)
        };

        Assert.Equal(action, result.Action);
        Assert.Equal(action != "kill", result.IsRunning);
    }

    [Fact]
    public async Task KillAsync_WhenMicroserviceFails_ThrowsWithDetail()
    {
        SetupOpenVpnServer();
        _handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("""{"success":false,"message":"openvpn busy"}""")
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSut().KillAsync(7, CancellationToken.None));

        Assert.Contains("openvpn busy", ex.Message);
    }

    [Fact]
    public async Task KillAsync_WhenNodeReturnsConflict_ThrowsBusyMessage()
    {
        SetupOpenVpnServer();
        _handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(
                """{"success":false,"message":"OpenVPN process operation already in progress. Please wait and try again."}""")
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSut().KillAsync(7, CancellationToken.None));

        Assert.Contains("already in progress", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StatusAsync_WhenNodeReturns404_ThrowsUpgradeHint()
    {
        SetupOpenVpnServer();
        _handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(
                """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Page Not Found","status":404,"detail":"The requested resource was not found."}""")
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSut().GetStatusAsync(7, CancellationToken.None));

        Assert.Contains("1.2.5.86", ex.Message);
        Assert.Contains("does not support process control", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task KillAsync_WhenAnotherCallInFlight_ThrowsBusyWithoutCallingNode()
    {
        SetupOpenVpnServer(id: 42);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _handler.Enqueue(_ =>
        {
            entered.TrySetResult();
            release.Task.GetAwaiter().GetResult();
            return OkProcess("kill", running: false, pid: null);
        });

        var sut = CreateSut();
        var first = Task.Run(() => sut.KillAsync(42, CancellationToken.None));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var busy = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.KillAsync(42, CancellationToken.None));
        Assert.Equal(VpnServerOpenVpnProcessService.BusyMessage, busy.Message);
        Assert.Single(_handler.Requests);

        release.TrySetResult();
        var result = await first;
        Assert.Equal("kill", result.Action);
    }

    [Fact]
    public async Task GetStatusAsync_WhenEnvelopeUnsuccessful_Throws()
    {
        SetupOpenVpnServer();
        _handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"success":false,"message":"not ready"}""")
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSut().GetStatusAsync(7, CancellationToken.None));
        Assert.Contains("not ready", ex.Message);
    }

    private sealed class QueueHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();
        public IList<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();

        public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> factory) => _responses.Enqueue(factory);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (_responses.Count == 0)
                throw new InvalidOperationException("No queued HTTP response.");
            return Task.FromResult(_responses.Dequeue()(request));
        }
    }
}
