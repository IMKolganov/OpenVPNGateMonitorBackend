using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using DataGateMonitor.Services.Helpers;

namespace DataGateMonitor.Tests.Services.Helpers;

public class ExternalIpAddressServiceTests
{
    private readonly Mock<ILogger<ExternalIpAddressService>> _logger = new();

    private static IConfiguration BuildConfigWithoutServices()
    {
        var dict = new Dictionary<string, string?>();
        return new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();
    }

    private static IConfiguration BuildConfigWithServices(params string[] urls)
    {
        var dict = new Dictionary<string, string?>();
        for (var i = 0; i < urls.Length; i++)
        {
            dict[$"ExternalIpServices:{i}"] = urls[i];
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();
    }

    [Fact]
    public async Task GetRemoteIpAddress_WhenNoServicesConfigured_ReturnsLoopback()
    {
        var config = BuildConfigWithoutServices();
        var httpClient = new HttpClient(new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

        var service = new ExternalIpAddressService(_logger.Object, config, httpClient);

        var result = await service.GetRemoteIpAddress(CancellationToken.None);

        Assert.Equal("127.0.0.1", result);
    }

    [Fact]
    public async Task GetRemoteIpAddress_WhenSingleServiceReturnsIp_ReturnsTrimmedIp()
    {
        const string serviceUrl = "https://example.test/ip";
        const string rawIp = " 203.0.113.10 \n";

        var config = BuildConfigWithServices(serviceUrl);

        var handler = new FakeHttpMessageHandler((request, _) =>
        {
            Assert.Equal(serviceUrl, request.RequestUri!.ToString());

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(rawIp)
            };
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(handler);
        var service = new ExternalIpAddressService(_logger.Object, config, httpClient);

        var result = await service.GetRemoteIpAddress(CancellationToken.None);

        Assert.Equal("203.0.113.10", result);
    }

    [Fact]
    public async Task GetRemoteIpAddress_WhenFirstServiceFails_SecondSucceeds()
    {
        const string url1 = "https://service1.test/ip";
        const string url2 = "https://service2.test/ip";
        const string ip2 = "198.51.100.42";

        var config = BuildConfigWithServices(url1, url2);

        var handler = new FakeHttpMessageHandler((request, _) =>
        {
            var url = request.RequestUri!.ToString();
            if (url == url1)
            {
                throw new HttpRequestException("Service1 unavailable");
            }

            if (url == url2)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($" {ip2} ")
                };
                return Task.FromResult(response);
            }

            throw new InvalidOperationException("Unexpected URL");
        });

        var httpClient = new HttpClient(handler);
        var service = new ExternalIpAddressService(_logger.Object, config, httpClient);

        var result = await service.GetRemoteIpAddress(CancellationToken.None);

        Assert.Equal(ip2, result);
    }

    [Fact]
    public async Task GetRemoteIpAddress_WhenAllServicesFail_ReturnsLoopback()
    {
        const string url1 = "https://service1.test/ip";
        const string url2 = "https://service2.test/ip";

        var config = BuildConfigWithServices(url1, url2);

        var handler = new FakeHttpMessageHandler((request, _) =>
        {
            throw new HttpRequestException($"Failed to call {request.RequestUri}");
        });

        var httpClient = new HttpClient(handler);
        var service = new ExternalIpAddressService(_logger.Object, config, httpClient);

        var result = await service.GetRemoteIpAddress(CancellationToken.None);

        Assert.Equal("127.0.0.1", result);
    }

    [Fact]
    public async Task GetRemoteIpAddress_WhenHtmlReturned_UsesNextService()
    {
        const string url1 = "https://service1.test/ip";
        const string url2 = "https://service2.test/ip";

        var config = BuildConfigWithServices(url1, url2);
        var handler = new FakeHttpMessageHandler((request, _) =>
        {
            var body = request.RequestUri!.ToString() == url1
                ? "<html>error</html>"
                : "203.0.113.55";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            });
        });

        var result = await new ExternalIpAddressService(_logger.Object, config, new HttpClient(handler))
            .GetRemoteIpAddress(CancellationToken.None);

        Assert.Equal("203.0.113.55", result);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("::1")]
    [InlineData("<html>nope</html>")]
    [InlineData("not-an-ip")]
    public void TryParsePublicIp_RejectsInvalid(string raw)
    {
        Assert.False(ExternalIpAddressService.TryParsePublicIp(raw, out _));
    }

    [Theory]
    [InlineData("203.0.113.10", "203.0.113.10")]
    [InlineData("  198.51.100.1 \n", "198.51.100.1")]
    public void TryParsePublicIp_AcceptsPublicIpv4(string raw, string expected)
    {
        Assert.True(ExternalIpAddressService.TryParsePublicIp(raw, out var ip));
        Assert.Equal(expected, ip);
    }

    // Simple fake handler to control HttpClient responses in tests
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public FakeHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}
