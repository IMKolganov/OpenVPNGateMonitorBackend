using DataGateMonitor.Services.Helpers;
using Xunit;

namespace DataGateMonitor.Tests.Services.Helpers;

public class ClientEndpointHostTests
{
    [Theory]
    [InlineData("198.51.100.1:443", "198.51.100.1")]
    [InlineData("[2001:db8::1]:443", "2001:db8::1")]
    [InlineData("10.0.0.1", "10.0.0.1")]
    public void TryGetHostForGeoLookup_ParsesCommonForms(string input, string expected)
    {
        Assert.Equal(expected, ClientEndpointHost.TryGetHostForGeoLookup(input));
    }

    [Fact]
    public void TryGetHostForGeoLookup_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(ClientEndpointHost.TryGetHostForGeoLookup(null));
        Assert.Null(ClientEndpointHost.TryGetHostForGeoLookup("   "));
    }

    [Theory]
    [InlineData("172.20.0.2", true)]
    [InlineData("172.20.0.2:443", true)]
    [InlineData("10.1.2.3", true)]
    [InlineData("192.168.0.1:1", true)]
    [InlineData("127.0.0.1:9", true)]
    [InlineData("203.0.113.1:443", false)]
    [InlineData("hostname.example", false)]
    [InlineData(null, false)]
    [InlineData("fc00::1", true)]
    [InlineData("[fe80::abcd]:53", true)]
    [InlineData("2001:db8::1", false)]
    [InlineData("169.254.1.1", true)]
    [InlineData("100.64.0.1", true)]
    public void IsPrivateOrLoopbackEndpoint_Classifies(string? input, bool expected)
    {
        Assert.Equal(expected, ClientEndpointHost.IsPrivateOrLoopbackEndpoint(input));
    }
}
