using DataGateMonitor.Services.Helpers;

namespace DataGateMonitor.Tests.Services.Helpers;

public class PiHoleClientSubnetPrefixTests
{
    [Theory]
    [InlineData("10.80.0", "10.80.0.")]
    [InlineData("10.80.0.", "10.80.0.")]
    [InlineData(" 10.51.15 ", "10.51.15.")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_AppendsTrailingDotForIpv4Prefix(string? raw, string expected) =>
        Assert.Equal(expected, PiHoleClientSubnetPrefix.Normalize(raw));
}
