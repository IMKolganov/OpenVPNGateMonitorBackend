using Microsoft.Extensions.Logging;
using Moq;
using DataGateMonitor.DataBase.Services.Query.IssuedOvpnFileTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Models;
using DataGateMonitor.Models.Helpers;
using DataGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;
using DataGateMonitor.Services.Others;
using DataGateMonitor.Services.Users;
using DataGateMonitor.Services.Users.Interfaces;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerClients.Responses;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Tests.Services.Users;

public class FreeTierOpenVpnSessionEnforcementServiceTests
{
    private readonly Mock<IVpnServerQueryService> _vpnServerQuery = new();
    private readonly Mock<IOpenVpnClientService> _openVpnClientService = new();
    private readonly Mock<IIssuedOvpnFileQueryService> _issuedOvpnFileQuery = new();
    private readonly Mock<IUserQueryService> _userQuery = new();
    private readonly Mock<IFreeTierAccessComplianceService> _complianceService = new();
    private readonly Mock<IOpenVpnDisconnectExecutor> _disconnectExecutor = new();
    private readonly Mock<IFreeTierGraceDisconnectNotifier> _graceDisconnectNotifier = new();
    private readonly Mock<ISettingsService> _settingsService = new();

    private FreeTierOpenVpnSessionEnforcementService CreateSut()
        => new(
            _vpnServerQuery.Object,
            _openVpnClientService.Object,
            _issuedOvpnFileQuery.Object,
            _userQuery.Object,
            _complianceService.Object,
            _disconnectExecutor.Object,
            _graceDisconnectNotifier.Object,
            _settingsService.Object,
            Mock.Of<ILogger<FreeTierOpenVpnSessionEnforcementService>>());

    private void SetupEnforcementEnabled(bool revokeOnEnforcement = false)
    {
        _settingsService
            .Setup(s => s.GetValueAsync<string>($"{FreeTierAccessSettingsKeys.EnforceOpenVpnSessions}_Type", It.IsAny<CancellationToken>()))
            .ReturnsAsync("bool");
        _settingsService
            .Setup(s => s.GetValueAsync<bool>(FreeTierAccessSettingsKeys.EnforceOpenVpnSessions, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _settingsService
            .Setup(s => s.GetValueAsync<string>($"{FreeTierAccessSettingsKeys.RevokeOvpnOnEnforcement}_Type", It.IsAny<CancellationToken>()))
            .ReturnsAsync("bool");
        _settingsService
            .Setup(s => s.GetValueAsync<bool>(FreeTierAccessSettingsKeys.RevokeOvpnOnEnforcement, It.IsAny<CancellationToken>()))
            .ReturnsAsync(revokeOnEnforcement);
    }

    private void SetupSingleConnectedClient(int userId, string commonName = "cn1", string externalId = "ext1")
    {
        var server = new VpnServer { Id = 1, ServerType = VpnServerType.OpenVpn, IsDisable = false, IsDeleted = false };
        _vpnServerQuery.Setup(q => q.GetAll(false, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([server]);

        _openVpnClientService
            .Setup(s => s.GetClientsFromManagementAsync(server, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenVpnManagementStatusResult
            {
                Clients = [new VpnServerClient { CommonName = commonName, VpnServerId = 1 }],
            });

        _issuedOvpnFileQuery
            .Setup(q => q.GetExternalIdByCommonName(commonName, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(externalId);

        _userQuery
            .Setup(q => q.GetByExternalId(externalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, DisplayName = "u" });
    }

    private void SetupNonCompliant(int userId, string planName = QuotaPlanNames.Free)
    {
        _complianceService
            .Setup(s => s.EvaluateAccessForEnforcementAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FreeTierAccessComplianceResult
            {
                IsApplicable = true,
                IsCompliant = false,
                ActivePlanName = planName,
            });
    }

    [Fact]
    public async Task EnforceAsync_WhenDisconnectSucceeds_NotifiesUserAndRecordsOutcome()
    {
        SetupEnforcementEnabled();
        SetupSingleConnectedClient(userId: 42);
        SetupNonCompliant(42);

        _disconnectExecutor
            .Setup(e => e.ExecuteWithLogIdAsync(It.IsAny<OpenVpnDisconnectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new KillOpenVpnClientResponse { Success = true }, (int?)77));
        _graceDisconnectNotifier
            .Setup(n => n.NotifyAsync(42, QuotaPlanNames.Free, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FreeTierGraceDisconnectOutcome("telegram", true));

        var sut = CreateSut();
        var killed = await sut.EnforceAsync(CancellationToken.None);

        Assert.Equal(1, killed);
        _graceDisconnectNotifier.Verify(n => n.NotifyAsync(42, QuotaPlanNames.Free, It.IsAny<CancellationToken>()), Times.Once);
        _disconnectExecutor.Verify(
            e => e.UpdateNotificationOutcomeAsync(77, "telegram", true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnforceAsync_WhenDisconnectFails_DoesNotNotifyUser()
    {
        SetupEnforcementEnabled();
        SetupSingleConnectedClient(userId: 43);
        SetupNonCompliant(43);

        _disconnectExecutor
            .Setup(e => e.ExecuteWithLogIdAsync(It.IsAny<OpenVpnDisconnectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new KillOpenVpnClientResponse { Success = false }, (int?)78));

        var sut = CreateSut();
        var killed = await sut.EnforceAsync(CancellationToken.None);

        Assert.Equal(0, killed);
        _graceDisconnectNotifier.Verify(
            n => n.NotifyAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _disconnectExecutor.Verify(
            e => e.UpdateNotificationOutcomeAsync(
                It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnforceAsync_WhenUserNotFlaggedForEnforcement_DoesNotDisconnectOrNotify()
    {
        SetupEnforcementEnabled();
        SetupSingleConnectedClient(userId: 44);

        _complianceService
            .Setup(s => s.EvaluateAccessForEnforcementAsync(44, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FreeTierAccessComplianceResult { IsApplicable = true, IsCompliant = true });

        var sut = CreateSut();
        var killed = await sut.EnforceAsync(CancellationToken.None);

        Assert.Equal(0, killed);
        _disconnectExecutor.Verify(
            e => e.ExecuteWithLogIdAsync(It.IsAny<OpenVpnDisconnectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _graceDisconnectNotifier.Verify(
            n => n.NotifyAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnforceAsync_WhenLogWriteFailed_SkipsOutcomeUpdate()
    {
        SetupEnforcementEnabled();
        SetupSingleConnectedClient(userId: 46);
        SetupNonCompliant(46);

        _disconnectExecutor
            .Setup(e => e.ExecuteWithLogIdAsync(It.IsAny<OpenVpnDisconnectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new KillOpenVpnClientResponse { Success = true }, (int?)null));
        _graceDisconnectNotifier
            .Setup(n => n.NotifyAsync(46, QuotaPlanNames.Free, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FreeTierGraceDisconnectOutcome("email", true));

        var sut = CreateSut();
        var killed = await sut.EnforceAsync(CancellationToken.None);

        Assert.Equal(1, killed);
        _graceDisconnectNotifier.Verify(n => n.NotifyAsync(46, QuotaPlanNames.Free, It.IsAny<CancellationToken>()), Times.Once);
        _disconnectExecutor.Verify(
            e => e.UpdateNotificationOutcomeAsync(
                It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnforceAsync_WhenNotifierThrows_StillCountsKillAndDoesNotPropagate()
    {
        SetupEnforcementEnabled();
        SetupSingleConnectedClient(userId: 45);
        SetupNonCompliant(45);

        _disconnectExecutor
            .Setup(e => e.ExecuteWithLogIdAsync(It.IsAny<OpenVpnDisconnectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new KillOpenVpnClientResponse { Success = true }, (int?)79));
        _graceDisconnectNotifier
            .Setup(n => n.NotifyAsync(45, QuotaPlanNames.Free, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var sut = CreateSut();
        var killed = await sut.EnforceAsync(CancellationToken.None);

        Assert.Equal(1, killed);
        _disconnectExecutor.Verify(
            e => e.UpdateNotificationOutcomeAsync(
                It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
