using DataGateMonitor.Services.Helpers;
using Xunit;

namespace DataGateMonitor.Tests.Services.Helpers;

public class VpnServerApiUrlHelperTests
{
    [Fact]
    public void ResolveReportedRemoteIp_PrefersNodePublicIp()
    {
        var result = VpnServerApiUrlHelper.ResolveReportedRemoteIp(
            nodePublicIp: "212.147.232.154",
            configVpnServerIp: "10.0.0.1",
            apiUrl: "https://s5.datagateapp.com/");

        Assert.Equal("212.147.232.154", result);
    }

    [Fact]
    public void ResolveReportedRemoteIp_FallsBackToConfigIp()
    {
        var result = VpnServerApiUrlHelper.ResolveReportedRemoteIp(
            nodePublicIp: null,
            configVpnServerIp: "212.147.232.154",
            apiUrl: "https://s5.datagateapp.com/");

        Assert.Equal("212.147.232.154", result);
    }

    [Fact]
    public void ResolveReportedRemoteIp_FallsBackToApiUrlHost()
    {
        var result = VpnServerApiUrlHelper.ResolveReportedRemoteIp(
            nodePublicIp: " ",
            configVpnServerIp: null,
            apiUrl: "https://s5.datagateapp.com/");

        Assert.Equal("s5.datagateapp.com", result);
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("-")]
    [InlineData("N/A")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public void ResolveReportedRemoteIp_IgnoresUnusableNodePublicIp(string bad)
    {
        var result = VpnServerApiUrlHelper.ResolveReportedRemoteIp(
            nodePublicIp: bad,
            configVpnServerIp: "164.215.15.224",
            apiUrl: "http://example.test:5010/");

        Assert.Equal("164.215.15.224", result);
    }

    [Fact]
    public void ResolveReportedRemoteIp_IgnoresUnusableConfig_UsesApiUrl()
    {
        var result = VpnServerApiUrlHelper.ResolveReportedRemoteIp(
            nodePublicIp: null,
            configVpnServerIp: "0.0.0.0",
            apiUrl: "http://164.215.15.224:5010/");

        Assert.Equal("164.215.15.224", result);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public void ResolveReportedRemoteIp_IgnoresLoopbackConfig_UsesApiUrlHost(string loopback)
    {
        var result = VpnServerApiUrlHelper.ResolveReportedRemoteIp(
            nodePublicIp: null,
            configVpnServerIp: loopback,
            apiUrl: "https://s5.datagateapp.com/");

        Assert.Equal("s5.datagateapp.com", result);
    }

    [Fact]
    public void ResolveReportedRemoteIp_AcceptsPublicIpv6()
    {
        var result = VpnServerApiUrlHelper.ResolveReportedRemoteIp(
            nodePublicIp: "2001:db8::1",
            configVpnServerIp: "1.2.3.4",
            apiUrl: "https://s5.datagateapp.com/");

        Assert.Equal("2001:db8::1", result);
    }

    [Fact]
    public void ResolveReportedRemoteIp_WhenNothingUsable_ReturnsEmpty()
    {
        var result = VpnServerApiUrlHelper.ResolveReportedRemoteIp(null, null, "not-a-url");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void TryHostFromApiUrl_ParsesAbsoluteHttpUrl()
    {
        Assert.Equal("s5.datagateapp.com", VpnServerApiUrlHelper.TryHostFromApiUrl("https://s5.datagateapp.com/"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("relative/path")]
    public void TryHostFromApiUrl_RejectsInvalid(string? url)
    {
        Assert.Null(VpnServerApiUrlHelper.TryHostFromApiUrl(url));
    }

    [Fact]
    public void ResolveReportedRemoteIp_TruncatesLongValues()
    {
        var longIp = new string('1', 300);
        var result = VpnServerApiUrlHelper.ResolveReportedRemoteIp(longIp, null, null);
        Assert.Equal(255, result.Length);
    }

    [Theory]
    [InlineData("https://xs1-hel.datagateapp.com", "xs1-hel.datagateapp.com")]
    [InlineData("https://xs1-hel.datagateapp.com:9443/", "xs1-hel.datagateapp.com")]
    [InlineData("xs1-hel.datagateapp.com:443", "xs1-hel.datagateapp.com")]
    public void SanitizeExportEndpointHost_StripsSchemeAndPort(string input, string expected)
    {
        Assert.Equal(expected, VpnServerApiUrlHelper.SanitizeExportEndpointHost(input));
    }

    [Fact]
    public void ResolveReportedRemoteIp_IgnoresMistakenHttpsConfig_UsesApiUrlHost()
    {
        var result = VpnServerApiUrlHelper.ResolveReportedRemoteIp(
            nodePublicIp: null,
            configVpnServerIp: "https://xs1-hel.datagateapp.com",
            apiUrl: "https://xs1-hel.datagateapp.com:9443/");

        Assert.Equal("xs1-hel.datagateapp.com", result);
    }
}
