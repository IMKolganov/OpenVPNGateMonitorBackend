using DataGateMonitor.DataBase.Services.Query.IssuedOvpnFileTable;
using DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.CertExpiry;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.Services.Others;
using DataGateMonitor.Services.Others.Notifications.CertExpiry;
using DataGateMonitor.SharedModels.DataGateMonitor.CertExpiry.Requests;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.Cert.Responses;
using DataGateMonitor.SharedModels.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DataGateMonitor.Tests.Services.CertExpiry;

public sealed class CertExpiryManualCheckTests : IDisposable
{
    private readonly string? _previousDisabledEnv;
    private readonly Mock<IIssuedOvpnFileQueryService> _issuedFiles = new();
    private readonly Mock<IVpnServerQueryService> _servers = new();
    private readonly Mock<ICertApiClient> _certApi = new();
    private readonly Mock<ICertExpiryNotificationService> _notifier = new();
    private readonly Mock<ISettingsService> _settings = new();
    private readonly CertExpiryNotificationTracker _tracker = new();
    private readonly CertExpiryRunHistoryStore _history = new();

    public CertExpiryManualCheckTests()
    {
        _previousDisabledEnv = Environment.GetEnvironmentVariable(CertExpiryEnvironment.DisabledVariable);
        Environment.SetEnvironmentVariable(CertExpiryEnvironment.DisabledVariable, null);
        _settings.Setup(s => s.GetValueAsync<int>(CertExpiryScheduledCheckRunner.WarningDaysSetting, It.IsAny<CancellationToken>()))
            .ReturnsAsync(30);
    }

    public void Dispose() =>
        Environment.SetEnvironmentVariable(CertExpiryEnvironment.DisabledVariable, _previousDisabledEnv);

    [Fact]
    public async Task RunCheckAsync_ManualRun_DoesNotSendNotificationsByDefault()
    {
        const int serverId = 10;
        SetupServer(serverId);
        SetupIssuedFile(serverId, "client-expiring");
        SetupNodeCert(serverId, "client-expiring", DateTimeOffset.UtcNow.AddDays(7));

        var result = await CreateRunner().RunCheckAsync(
            new RunCertExpiryCheckRequest { VpnServerId = serverId, SendNotifications = false },
            CancellationToken.None);

        Assert.Equal(CertExpiryRunStatus.Completed, result.Status);
        Assert.Equal(1, result.Summary.ExpiringSoon);
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
    public async Task RunCheckAsync_StoresDetailedServerAndProfileResults()
    {
        const int serverId = 10;
        SetupServer(serverId);
        SetupIssuedFile(serverId, "client-expired");
        SetupNodeCert(serverId, "client-expired", DateTimeOffset.UtcNow.AddDays(-1));

        var runner = CreateRunner();
        var result = await runner.RunCheckAsync(
            new RunCertExpiryCheckRequest { VpnServerId = serverId },
            CancellationToken.None);

        Assert.Single(result.Servers);
        Assert.Equal(CertExpiryServerFetchStatus.Success, result.Servers[0].FetchStatus);
        Assert.Single(result.Servers[0].Profiles);
        Assert.Equal(CertExpiryProfileOutcome.Expired, result.Servers[0].Profiles[0].Outcome);
        Assert.Equal("client-expired", result.Servers[0].Profiles[0].CommonName);

        var fromHistory = _history.Get(result.RunId);
        Assert.NotNull(fromHistory);
        Assert.Equal(result.RunId, fromHistory!.RunId);
    }

    [Fact]
    public async Task RunCheckAsync_WhenServerFetchFails_RecordsFetchError()
    {
        const int serverId = 10;
        SetupServer(serverId);
        SetupIssuedFile(serverId, "client-1");
        _certApi.Setup(c => c.GetAllCertificatesAsync(serverId, It.IsAny<CancellationToken>(), false))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var result = await CreateRunner().RunCheckAsync(
            new RunCertExpiryCheckRequest { VpnServerId = serverId },
            CancellationToken.None);

        Assert.Equal(CertExpiryRunStatus.Completed, result.Status);
        Assert.Equal(1, result.Summary.ServerFailures);
        Assert.Equal("Connection refused", result.Servers[0].FetchError);
        Assert.Empty(result.Servers[0].Profiles);
    }

    [Fact]
    public async Task RunCheckAsync_WhenAlreadyRunning_ReturnsSkipped()
    {
        var runner = CreateRunner();
        var tcs = new TaskCompletionSource<List<VpnServer>>();
        _servers.Setup(s => s.GetAll(false, false, null, It.IsAny<UserVpnServerAccessOverrides?>(), It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        var first = runner.RunCheckAsync(new RunCertExpiryCheckRequest(), CancellationToken.None);
        await Task.Delay(100);
        var second = await runner.RunCheckAsync(new RunCertExpiryCheckRequest(), CancellationToken.None);

        Assert.Equal(CertExpiryRunStatus.SkippedAlreadyRunning, second.Status);
        tcs.SetResult([]);
        await first;
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
