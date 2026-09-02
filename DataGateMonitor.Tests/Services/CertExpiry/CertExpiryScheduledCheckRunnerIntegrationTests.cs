using DataGateMonitor.DataBase.Services.Query.IssuedOvpnFileTable;
using DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.CertExpiry;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.Services.Others;
using DataGateMonitor.Services.Others.Notifications.CertExpiry;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.Cert.Responses;
using DataGateMonitor.SharedModels.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DataGateMonitor.Tests.Services.CertExpiry;

public sealed class CertExpiryScheduledCheckRunnerIntegrationTests : IDisposable
{
    private readonly string? _previousDisabledEnv;
    private readonly Mock<IIssuedOvpnFileQueryService> _issuedFiles = new();
    private readonly Mock<IVpnServerQueryService> _servers = new();
    private readonly Mock<ICertApiClient> _certApi = new();
    private readonly Mock<ICertExpiryNotificationService> _notifier = new();
    private readonly Mock<ISettingsService> _settings = new();
    private readonly CertExpiryNotificationTracker _tracker = new();
    private readonly CertExpiryRunHistoryStore _history = new();

    public CertExpiryScheduledCheckRunnerIntegrationTests()
    {
        _previousDisabledEnv = Environment.GetEnvironmentVariable(CertExpiryEnvironment.DisabledVariable);
        Environment.SetEnvironmentVariable(CertExpiryEnvironment.DisabledVariable, null);
        _settings.Setup(s => s.GetValueAsync<int>(CertExpiryScheduledCheckRunner.WarningDaysSetting, It.IsAny<CancellationToken>()))
            .ReturnsAsync(30);
    }

    public void Dispose() =>
        Environment.SetEnvironmentVariable(CertExpiryEnvironment.DisabledVariable, _previousDisabledEnv);

    [Fact]
    public async Task RunAsync_WhenNoEligibleServers_DoesNotQueryCertificatesOrProfiles()
    {
        _servers.Setup(s => s.GetAll(false, false, null, It.IsAny<UserVpnServerAccessOverrides?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await CreateRunner().RunAsync(CancellationToken.None);

        _issuedFiles.Verify(q => q.GetAllActiveByVpnServerIds(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        _certApi.Verify(
            c => c.GetAllCertificatesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenEligibleServersButNoProfiles_DoesNotCallCertApi()
    {
        const int serverId = 99;
        SetupServer(serverId);
        _issuedFiles.Setup(q => q.GetAllActiveByVpnServerIds(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await CreateRunner().RunAsync(CancellationToken.None);

        _certApi.Verify(
            c => c.GetAllCertificatesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenCertExpiringSoon_NotifiesOnce()
    {
        const int serverId = 10;
        SetupServer(serverId);
        SetupIssuedFile(serverId, "client-expiring");
        SetupNodeCert(serverId, "client-expiring", DateTimeOffset.UtcNow.AddDays(7));

        var runner = CreateRunner();
        await runner.RunAsync(CancellationToken.None);
        await runner.RunAsync(CancellationToken.None);

        _notifier.Verify(
            n => n.NotifyExpiringSoonAsync(
                It.Is<IssuedOvpnFile>(f => f.CommonName == "client-expiring"),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenCertExpired_NotifiesExpired()
    {
        const int serverId = 11;
        SetupServer(serverId);
        SetupIssuedFile(serverId, "client-expired");
        SetupNodeCert(serverId, "client-expired", DateTimeOffset.UtcNow.AddDays(-2));

        await CreateRunner().RunAsync(CancellationToken.None);

        _notifier.Verify(
            n => n.NotifyExpiredAsync(
                It.Is<IssuedOvpnFile>(f => f.CommonName == "client-expired"),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _notifier.Verify(
            n => n.NotifyExpiringSoonAsync(
                It.IsAny<IssuedOvpnFile>(),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenCertMissingOnNode_NotifiesMissing()
    {
        const int serverId = 12;
        SetupServer(serverId);
        SetupIssuedFile(serverId, "missing-client");
        _certApi.Setup(c => c.GetAllCertificatesAsync(serverId, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync([]);

        await CreateRunner().RunAsync(CancellationToken.None);

        _notifier.Verify(
            n => n.NotifyCertificateMissingAsync(
                It.Is<IssuedOvpnFile>(f => f.CommonName == "missing-client"),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenServerFetchFails_NotifiesOncePerServer()
    {
        const int serverId = 13;
        SetupServer(serverId);
        SetupIssuedFile(serverId, "any");
        _certApi.Setup(c => c.GetAllCertificatesAsync(serverId, It.IsAny<CancellationToken>(), false))
            .ThrowsAsync(new HttpRequestException("node offline"));

        var runner = CreateRunner();
        await runner.RunAsync(CancellationToken.None);
        await runner.RunAsync(CancellationToken.None);

        _notifier.Verify(
            n => n.NotifyServerCheckFailedAsync(serverId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_SkipsDisabledAndNonOpenVpnServers()
    {
        _servers.Setup(s => s.GetAll(false, false, null, It.IsAny<UserVpnServerAccessOverrides?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new VpnServer
                {
                    Id = 1,
                    ServerName = "Xray",
                    ServerType = VpnServerType.Xray,
                    ApiUrl = "http://xray"
                },
                new VpnServer
                {
                    Id = 2,
                    ServerName = "Disabled",
                    ServerType = VpnServerType.OpenVpn,
                    ApiUrl = "http://ovpn",
                    IsDisable = true
                }
            ]);

        await CreateRunner().RunAsync(CancellationToken.None);

        _issuedFiles.Verify(
            q => q.GetAllActiveByVpnServerIds(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _certApi.Verify(
            c => c.GetAllCertificatesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenCertOutsideWarningWindow_DoesNotNotify()
    {
        const int serverId = 14;
        SetupServer(serverId);
        SetupIssuedFile(serverId, "healthy-client");
        SetupNodeCert(serverId, "healthy-client", DateTimeOffset.UtcNow.AddDays(120));

        await CreateRunner().RunAsync(CancellationToken.None);

        _notifier.Verify(
            n => n.NotifyExpiringSoonAsync(
                It.IsAny<IssuedOvpnFile>(),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _notifier.Verify(
            n => n.NotifyExpiredAsync(
                It.IsAny<IssuedOvpnFile>(),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_UsesGetAllActiveByVpnServerIds_NotFullTableScan()
    {
        const int serverId = 16;
        SetupServer(serverId);
        SetupIssuedFile(serverId, "client");

        await CreateRunner().RunAsync(CancellationToken.None);

        _issuedFiles.Verify(
            q => q.GetAllActiveByVpnServerIds(
                It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(serverId)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _issuedFiles.Verify(q => q.GetAllActive(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenCustomWarningDays_UsesSettingValue()
    {
        const int serverId = 17;
        _settings.Setup(s => s.GetValueAsync<int>(CertExpiryScheduledCheckRunner.WarningDaysSetting, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);
        SetupServer(serverId);
        SetupIssuedFile(serverId, "client-warning-7d");
        SetupNodeCert(serverId, "client-warning-7d", DateTimeOffset.UtcNow.AddDays(5));

        await CreateRunner().RunAsync(CancellationToken.None);

        _notifier.Verify(
            n => n.NotifyExpiringSoonAsync(
                It.Is<IssuedOvpnFile>(f => f.CommonName == "client-warning-7d"),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenCertRevokedOnNode_NotifiesExpired()
    {
        const int serverId = 18;
        SetupServer(serverId);
        SetupIssuedFile(serverId, "revoked-on-node");
        _certApi.Setup(c => c.GetAllCertificatesAsync(serverId, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(
            [
                new ServerCertificate
                {
                    CommonName = "revoked-on-node",
                    ExpiryDate = DateTimeOffset.UtcNow.AddDays(90),
                    Status = CertificateStatus.Revoked,
                    IsRevoked = true,
                    SerialNumber = "rev"
                }
            ]);

        await CreateRunner().RunAsync(CancellationToken.None);

        _notifier.Verify(
            n => n.NotifyExpiredAsync(
                It.Is<IssuedOvpnFile>(f => f.CommonName == "revoked-on-node"),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_UsesSilentCertificateFetch()
    {
        const int serverId = 15;
        SetupServer(serverId);
        SetupIssuedFile(serverId, "client");
        SetupNodeCert(serverId, "client", DateTimeOffset.UtcNow.AddDays(120));

        await CreateRunner().RunAsync(CancellationToken.None);

        _certApi.Verify(
            c => c.GetAllCertificatesAsync(serverId, It.IsAny<CancellationToken>(), false),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenDisabledViaEnvironment_DoesNothing()
    {
        Environment.SetEnvironmentVariable(CertExpiryEnvironment.DisabledVariable, "true");
        SetupServer(1);
        SetupIssuedFile(1, "client");

        await CreateRunner().RunAsync(CancellationToken.None);

        _servers.Verify(s => s.GetAll(false, false, null, It.IsAny<UserVpnServerAccessOverrides?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private CertExpiryScheduledCheckRunner CreateRunner()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _issuedFiles.Object);
        services.AddScoped(_ => _servers.Object);
        services.AddScoped(_ => _certApi.Object);
        services.AddScoped(_ => _notifier.Object);
        services.AddScoped(_ => _settings.Object);

        var provider = services.BuildServiceProvider();
        return new CertExpiryScheduledCheckRunner(
            NullLogger<CertExpiryScheduledCheckRunner>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>(),
            _tracker,
            _history);
    }

    private void SetupServer(int serverId)
    {
        _servers.Setup(s => s.GetAll(false, false, null, It.IsAny<UserVpnServerAccessOverrides?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new VpnServer
                {
                    Id = serverId,
                    ServerName = $"Server-{serverId}",
                    ServerType = VpnServerType.OpenVpn,
                    ApiUrl = $"http://server-{serverId}"
                }
            ]);
    }

    private void SetupIssuedFile(int serverId, string commonName)
    {
        _issuedFiles.Setup(q => q.GetAllActiveByVpnServerIds(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new IssuedOvpnFile
                {
                    Id = commonName.GetHashCode(),
                    VpnServerId = serverId,
                    CommonName = commonName,
                    ExternalId = "ext",
                    IssuedAt = DateTimeOffset.UtcNow.AddMonths(-1),
                    IsRevoked = false
                }
            ]);
    }

    private void SetupNodeCert(int serverId, string commonName, DateTimeOffset expiry)
    {
        _certApi.Setup(c => c.GetAllCertificatesAsync(serverId, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(
            [
                new ServerCertificate
                {
                    CommonName = commonName,
                    ExpiryDate = expiry,
                    SerialNumber = "serial-1",
                    Status = expiry <= DateTimeOffset.UtcNow ? CertificateStatus.Expired : CertificateStatus.Active
                }
            ]);
    }
}
