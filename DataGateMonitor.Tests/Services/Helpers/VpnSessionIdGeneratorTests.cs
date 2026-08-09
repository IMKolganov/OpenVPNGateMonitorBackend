using DataGateMonitor.Services.Helpers;

namespace DataGateMonitor.Tests.Services.Helpers;

public class VpnSessionIdGeneratorTests
{
    [Fact]
    public void FromCommonNameRemoteConnectedSince_IsDeterministic()
    {
        var since = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var a = VpnSessionIdGenerator.FromCommonNameRemoteConnectedSince("cn", "1.2.3.4:1194", since);
        var b = VpnSessionIdGenerator.FromCommonNameRemoteConnectedSince("cn", "1.2.3.4:1194", since);
        Assert.Equal(a, b);
    }

    [Fact]
    public void FromCommonNameRemoteConnectedSince_ChangesWithConnectedSince()
    {
        var a = VpnSessionIdGenerator.FromCommonNameRemoteConnectedSince(
            "cn", "1.2.3.4", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var b = VpnSessionIdGenerator.FromCommonNameRemoteConnectedSince(
            "cn", "1.2.3.4", new DateTimeOffset(2024, 1, 1, 0, 0, 1, TimeSpan.Zero));
        Assert.NotEqual(a, b);
    }
}
