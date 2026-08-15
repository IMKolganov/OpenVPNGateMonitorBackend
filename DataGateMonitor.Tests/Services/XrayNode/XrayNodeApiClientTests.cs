using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.Services.XrayNode;
using Xunit;

namespace DataGateMonitor.Tests.Services.XrayNode;

public class XrayNodeApiClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_respond(request));
    }

    [Fact]
    public async Task GetActiveClientsAsync_ReturnsDeserializedClients_On200()
    {
        const string json = """
            {
              "success": true,
              "message": "Success",
              "data": {
                "clients": [
                  {
                    "email": "a@b.c",
                    "remoteAddress": "1.1.1.1:1",
                    "bytesReceived": 1,
                    "bytesSent": 2,
                    "connectedSince": "2024-01-02T03:04:05Z"
                  }
                ],
                "polledAt": "2024-01-02T03:04:05Z"
              }
            }
            """;

        var handler = new StubHandler(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.EndsWith("/api/xray/clients", req.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(XrayNodeApiClient.HttpClientName))
            .Returns(new HttpClient(handler) { BaseAddress = new Uri("https://agent.example/") });

        var token = new Mock<IMicroserviceTokenService>();
        token.Setup(t => t.GenerateToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test-jwt");

        var sut = new XrayNodeApiClient(NullLogger<XrayNodeApiClient>.Instance, factory.Object, token.Object);

        var result = await sut.GetActiveClientsAsync("https://agent.example/", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result!.Clients);
        Assert.Equal("a@b.c", result.Clients[0].Email);
        Assert.Equal(1, result.Clients[0].BytesReceived);
        Assert.Equal(2, result.Clients[0].BytesSent);
    }

    [Fact]
    public async Task GetActiveClientsAsync_MapsProxyRealIp()
    {
        const string json = """
            {
              "success": true,
              "message": "Success",
              "data": {
                "clients": [
                  {
                    "email": "a@b.c",
                    "remoteAddress": "172.20.0.2",
                    "proxyRealIp": "203.0.113.9:443",
                    "bytesReceived": 1,
                    "bytesSent": 2,
                    "connectedSince": "2024-01-02T03:04:05Z"
                  }
                ],
                "polledAt": "2024-01-02T03:04:05Z"
              }
            }
            """;

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(XrayNodeApiClient.HttpClientName))
            .Returns(new HttpClient(handler) { BaseAddress = new Uri("https://agent.example/") });

        var token = new Mock<IMicroserviceTokenService>();
        token.Setup(t => t.GenerateToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test-jwt");

        var sut = new XrayNodeApiClient(NullLogger<XrayNodeApiClient>.Instance, factory.Object, token.Object);
        var result = await sut.GetActiveClientsAsync("https://agent.example/", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("172.20.0.2", result!.Clients[0].RemoteAddress);
        Assert.Equal("203.0.113.9:443", result.Clients[0].ProxyRealIp);
    }

    [Fact]
    public async Task GetActiveClientsAsync_ReturnsNull_OnNonSuccess()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(XrayNodeApiClient.HttpClientName))
            .Returns(new HttpClient(handler));

        var token = new Mock<IMicroserviceTokenService>();
        token.Setup(t => t.GenerateToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test-jwt");

        var sut = new XrayNodeApiClient(NullLogger<XrayNodeApiClient>.Instance, factory.Object, token.Object);

        var result = await sut.GetActiveClientsAsync("https://agent.example/", CancellationToken.None);

        Assert.Null(result);
    }
}
