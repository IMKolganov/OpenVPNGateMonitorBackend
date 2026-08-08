using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerStatusLogTable;
using DataGateMonitor.Models;
using DataGateMonitor.Models.XrayNode;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.Services.XrayNode;
using DataGateMonitor.SharedModels.Enums;
using Microsoft.Extensions.Logging;
using Moq;

namespace DataGateMonitor.Tests.Services.XrayNode;

public class XrayVpnServerStatusLogServicePublicIpTests
{
    private readonly Mock<ILogger<XrayVpnServerStatusLogService>> _logger = new();
    private readonly Mock<IVpnServerOvpnFileConfigQueryService> _config = new();
    private readonly Mock<IVpnNodePublicIpLookup> _publicIp = new();
    private readonly Mock<IVpnServerStatusLogQueryService> _statusQuery = new();
    private readonly Mock<ICommandService<VpnServerStatusLog, int>> _statusCmd = new();

    private XrayVpnServerStatusLogService CreateSut() => new(
        _logger.Object,
        _config.Object,
        _publicIp.Object,
        _statusQuery.Object,
        _statusCmd.Object);

    private static VpnServer Server() => new()
    {
        Id = 11,
        ServerName = "Xray-1",
        ApiUrl = "https://x1.datagateapp.com/",
        ServerType = VpnServerType.Xray
    };

    private static XrayNodeClientsResponse Payload() => new()
    {
        Clients = [],
        Server = new XrayNodeServerSnapshotDto
        {
            UpSince = DateTimeOffset.UtcNow.AddHours(-2),
            Version = "1.8.0",
            ServerLocalIp = "10.0.0.1",
            BytesIn = 1,
            BytesOut = 2
        }
    };

    [Fact]
    public async Task TryAppendOrUpdateAsync_UsesNodePublicIp()
    {
        _statusQuery
            .Setup(q => q.GetBySessionIdAndVpnServerId(It.IsAny<Guid>(), 11, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServerStatusLog?)null);
        _config.Setup(c => c.GetByVpnServerIdId(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServerOvpnFileConfig { VpnServerId = 11, VpnServerIp = "1.2.3.4" });
        _publicIp.Setup(p => p.GetAsync(11, VpnServerType.Xray, It.IsAny<CancellationToken>()))
            .ReturnsAsync("203.0.113.77");

        VpnServerStatusLog? saved = null;
        _statusCmd
            .Setup(c => c.Add(It.IsAny<VpnServerStatusLog>(), true, It.IsAny<CancellationToken>()))
            .Callback<VpnServerStatusLog, bool, CancellationToken>((e, _, _) => saved = e)
            .ReturnsAsync((VpnServerStatusLog e, bool _, CancellationToken _) => e);

        await CreateSut().TryAppendOrUpdateAsync(Server(), Payload(), CancellationToken.None);

        Assert.Equal("203.0.113.77", saved!.ServerRemoteIp);
        Assert.Equal("10.0.0.1", saved.ServerLocalIp);
    }

    [Fact]
    public async Task TryAppendOrUpdateAsync_WhenLookupReturnsNull_FallsBackToConfigIp()
    {
        _statusQuery
            .Setup(q => q.GetBySessionIdAndVpnServerId(It.IsAny<Guid>(), 11, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServerStatusLog?)null);
        _config.Setup(c => c.GetByVpnServerIdId(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServerOvpnFileConfig { VpnServerId = 11, VpnServerIp = "9.9.9.9" });
        _publicIp.Setup(p => p.GetAsync(11, VpnServerType.Xray, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        VpnServerStatusLog? saved = null;
        _statusCmd
            .Setup(c => c.Add(It.IsAny<VpnServerStatusLog>(), true, It.IsAny<CancellationToken>()))
            .Callback<VpnServerStatusLog, bool, CancellationToken>((e, _, _) => saved = e)
            .ReturnsAsync((VpnServerStatusLog e, bool _, CancellationToken _) => e);

        await CreateSut().TryAppendOrUpdateAsync(Server(), Payload(), CancellationToken.None);

        Assert.Equal("9.9.9.9", saved!.ServerRemoteIp);
    }

    [Fact]
    public async Task TryAppendOrUpdateAsync_WhenNoPublicIpOrConfig_FallsBackToApiUrlHost()
    {
        _statusQuery
            .Setup(q => q.GetBySessionIdAndVpnServerId(It.IsAny<Guid>(), 11, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServerStatusLog?)null);
        _config.Setup(c => c.GetByVpnServerIdId(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServerOvpnFileConfig?)null);
        _publicIp.Setup(p => p.GetAsync(11, VpnServerType.Xray, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        VpnServerStatusLog? saved = null;
        _statusCmd
            .Setup(c => c.Add(It.IsAny<VpnServerStatusLog>(), true, It.IsAny<CancellationToken>()))
            .Callback<VpnServerStatusLog, bool, CancellationToken>((e, _, _) => saved = e)
            .ReturnsAsync((VpnServerStatusLog e, bool _, CancellationToken _) => e);

        await CreateSut().TryAppendOrUpdateAsync(Server(), Payload(), CancellationToken.None);

        Assert.Equal("x1.datagateapp.com", saved!.ServerRemoteIp);
    }

    [Fact]
    public async Task TryAppendOrUpdateAsync_UpdatesExistingRow()
    {
        var existing = new VpnServerStatusLog
        {
            Id = 5,
            VpnServerId = 11,
            ServerRemoteIp = "old"
        };
        _statusQuery
            .Setup(q => q.GetBySessionIdAndVpnServerId(It.IsAny<Guid>(), 11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _config.Setup(c => c.GetByVpnServerIdId(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServerOvpnFileConfig?)null);
        _publicIp.Setup(p => p.GetAsync(11, VpnServerType.Xray, It.IsAny<CancellationToken>()))
            .ReturnsAsync("198.51.100.1");
        _statusCmd
            .Setup(c => c.Update(existing, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await CreateSut().TryAppendOrUpdateAsync(Server(), Payload(), CancellationToken.None);

        Assert.Equal("198.51.100.1", existing.ServerRemoteIp);
        _statusCmd.Verify(c => c.Update(existing, true, It.IsAny<CancellationToken>()), Times.Once);
    }
}
