using DataGateMonitor.Services.VpnManagerReleases;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Responses;
using DataGateMonitor.SharedModels.Enums;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace DataGateMonitor.Tests.Services.VpnManagerReleases;

public class VpnManagerReleaseLatestServiceTests
{
    [Theory]
    [InlineData("v1.2.5.103", "1.2.5.103")]
    [InlineData("V1.1.2.24", "1.1.2.24")]
    [InlineData("1.2.5.103", "1.2.5.103")]
    [InlineData("  v9.0.0  ", "9.0.0")]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("v", "v")]
    public void NormalizeVersionTag_StripsLeadingVWhenFollowedByDigit(string? raw, string? expected)
    {
        Assert.Equal(expected, VpnManagerReleaseLatestService.NormalizeVersionTag(raw));
    }

    [Theory]
    [InlineData("1.2.5.100", "1.2.5.103", true)]
    [InlineData("1.2.5.103", "1.2.5.103", false)]
    [InlineData("1.2.5.104", "1.2.5.103", false)]
    [InlineData(null, "1.2.5.103", false)]
    [InlineData("1.2.5.100", null, false)]
    [InlineData("v1.0.0", "v1.0.1", true)]
    public void IsUpdateAvailable_ComparesNormalizedVersions(string? installed, string? latest, bool expected)
    {
        Assert.Equal(expected, VpnManagerReleaseLatestService.IsUpdateAvailable(installed, latest));
    }

    [Fact]
    public async Task GetLatestAsync_ParsesGitHubPayload_AndCaches()
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r =>
                    r.Method == HttpMethod.Get
                    && r.RequestUri!.AbsolutePath.Contains("/releases/latest")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"tag_name":"v1.2.5.103","html_url":"https://github.com/IMKolganov/DataGateCertManager/releases/tag/v1.2.5.103"}""")
            })
            .Verifiable();

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(VpnManagerReleaseLatestService.HttpClientName))
            .Returns(() =>
            {
                var client = new HttpClient(handler.Object)
                {
                    BaseAddress = new Uri("https://api.github.com/")
                };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("DataGateMonitor-Tests");
                return client;
            });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var opts = Options.Create(new VpnManagerReleasesOptions { CacheMinutes = 30 });
        var sut = new VpnManagerReleaseLatestService(
            factory.Object, cache, opts, NullLogger<VpnManagerReleaseLatestService>.Instance);

        var first = await sut.GetLatestAsync(VpnServerType.OpenVpn, CancellationToken.None);
        var second = await sut.GetLatestAsync(VpnServerType.OpenVpn, CancellationToken.None);

        Assert.NotNull(first);
        Assert.Equal("1.2.5.103", first!.Version);
        Assert.Equal(
            "https://github.com/IMKolganov/DataGateCertManager/releases/tag/v1.2.5.103",
            first.ReleaseUrl);
        Assert.Same(first, second);
        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }
}

public class VpnManagerUpdateStatusEnricherTests
{
    [Fact]
    public async Task EnrichAsync_MarksUpdateWhenInstalledBehindLatest()
    {
        var releases = new Mock<IVpnManagerReleaseLatestService>();
        releases.Setup(r => r.GetLatestAsync(VpnServerType.OpenVpn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnManagerReleaseLatestInfo
            {
                ServerType = VpnServerType.OpenVpn,
                Version = "1.2.5.103",
                ReleaseUrl = "https://example.com/r",
            });

        var sut = new VpnManagerUpdateStatusEnricher(releases.Object);
        var items = new List<VpnServerWithStatusDto>
        {
            new()
            {
                VpnServerResponses = new VpnServerResponse
                {
                    VpnServer = new VpnServerDto { Id = 1, ServerType = VpnServerType.OpenVpn }
                },
                InstalledManagerVersion = "1.2.5.100",
            },
            new()
            {
                VpnServerResponses = new VpnServerResponse
                {
                    VpnServer = new VpnServerDto { Id = 2, ServerType = VpnServerType.OpenVpn }
                },
                InstalledManagerVersion = "1.2.5.103",
            },
            new()
            {
                VpnServerResponses = new VpnServerResponse
                {
                    VpnServer = new VpnServerDto { Id = 3, ServerType = VpnServerType.OpenVpn }
                },
                InstalledManagerVersion = null,
            },
        };

        await sut.EnrichAsync(items, CancellationToken.None);

        Assert.True(items[0].IsManagerUpdateAvailable);
        Assert.Equal("1.2.5.103", items[0].LatestManagerVersion);
        Assert.Equal("https://example.com/r", items[0].ManagerReleaseUrl);

        Assert.False(items[1].IsManagerUpdateAvailable);
        Assert.Equal("1.2.5.103", items[1].LatestManagerVersion);

        Assert.False(items[2].IsManagerUpdateAvailable);
        Assert.Equal("1.2.5.103", items[2].LatestManagerVersion);
    }
}
