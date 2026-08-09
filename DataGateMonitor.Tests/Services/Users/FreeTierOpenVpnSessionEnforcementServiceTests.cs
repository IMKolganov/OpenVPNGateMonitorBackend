using DataGateMonitor.DataBase.Services.Query.IssuedOvpnFileTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;
using DataGateMonitor.Services.Others;
using DataGateMonitor.Services.Users;
using DataGateMonitor.Services.Users.Interfaces;
using DataGateMonitor.SharedModels.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DataGateMonitor.Tests.Services.Users;

public class FreeTierOpenVpnSessionEnforcementServiceTests
{
    private static FreeTierOpenVpnSessionEnforcementService CreateSut(Mock<ISettingsService> settings) =>
        new(
            Mock.Of<IVpnServerQueryService>(),
            Mock.Of<IOpenVpnClientService>(),
            Mock.Of<IIssuedOvpnFileQueryService>(),
            Mock.Of<IUserQueryService>(),
            Mock.Of<IFreeTierAccessComplianceService>(),
            Mock.Of<IOpenVpnDisconnectExecutor>(),
            Mock.Of<IFreeTierGraceDisconnectNotifier>(),
            settings.Object,
            NullLogger<FreeTierOpenVpnSessionEnforcementService>.Instance);

    [Fact]
    public async Task IsEnabledAsync_WhenTypeMissing_DefaultsTrue()
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetValueAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        Assert.True(await CreateSut(settings).IsEnabledAsync());
    }

    [Fact]
    public async Task GetIntervalMinutesAsync_WhenTypeMissing_Defaults15()
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetValueAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        Assert.Equal(15, await CreateSut(settings).GetIntervalMinutesAsync());
    }

    [Fact]
    public async Task EnforceAsync_WhenDisabled_ReturnsZero()
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetValueAsync<string>(It.Is<string>(k => k.Contains("Type")), It.IsAny<CancellationToken>()))
            .ReturnsAsync("bool");
        settings.Setup(s => s.GetValueAsync<bool>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Assert.Equal(0, await CreateSut(settings).EnforceAsync());
    }
}
