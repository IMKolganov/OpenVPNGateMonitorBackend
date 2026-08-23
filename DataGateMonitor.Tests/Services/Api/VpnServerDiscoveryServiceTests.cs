using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using DataGateMonitor.DataBase.Contexts;
using DataGateMonitor.DataBase.Repositories.Interfaces;
using DataGateMonitor.DataBase.Repositories.Queries.Interfaces;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query;
using DataGateMonitor.DataBase.UnitOfWork;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api;
using DataGateMonitor.Services.Api.Interfaces;
using DataGateMonitor.Services.Others.Notifications.ServerOpenVpnApiClient;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Requests;
using DataGateMonitor.SharedModels.Enums;
using DataGateMonitor.Tests.Helpers;

namespace DataGateMonitor.Tests.Services.Api;

/// <summary>
/// Covers discovery announce/approve/deny, coexistence with already-registered servers,
/// and post-approve announce of the same node.
/// </summary>
public class VpnServerDiscoveryServiceTests
{
    [Theory]
    [InlineData("http://10.0.0.1:5010", "http://10.0.0.1:5010/")]
    [InlineData("http://10.0.0.1:5010/", "http://10.0.0.1:5010/")]
    [InlineData("10.0.0.1:5010", "http://10.0.0.1:5010/")]
    [InlineData("https://vpn.example.com:9443/api", "https://vpn.example.com:9443/api/")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void NormalizeApiUrl_NormalizesOrRejectsEmpty(string? input, string expected)
    {
        Assert.Equal(expected, VpnServerDiscoveryService.NormalizeApiUrl(input));
    }

    [Fact]
    public async Task AnnounceAsync_Throws_WhenApiUrlMissing()
    {
        await using var ctx = CreateContext();
        var sut = CreateSut(ctx, out _, out _, out _);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.AnnounceAsync(new AnnounceVpnServerRequest { ApiUrl = "  " }, CancellationToken.None));

        Assert.Contains("ApiUrl", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnnounceAsync_ReturnsAlreadyRegistered_WhenActiveServerExists_CaseInsensitiveUrl()
    {
        await using var ctx = CreateContext();
        SeedServer(ctx, id: 5, name: "existing", apiUrl: "http://10.0.0.1:5010/", deleted: false);
        SeedServer(ctx, id: 6, name: "other", apiUrl: "http://10.0.0.99:5010/", deleted: false);
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx, out var discoveryCmd, out var notifications, out _);

        var result = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = "HTTP://10.0.0.1:5010", // different case / no trailing slash
            SuggestedName = "node-a"
        }, CancellationToken.None);

        Assert.Equal(AnnounceVpnServerResultStatus.AlreadyRegistered, result.Status);
        Assert.Equal(5, result.ExistingVpnServerId);
        Assert.Null(result.DiscoveryId);
        Assert.Empty(ctx.VpnServerDiscoveries);
        discoveryCmd.Verify(c => c.Add(It.IsAny<VpnServerDiscovery>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        notifications.Verify(
            n => n.NotifyDiscovered(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Existing fleet untouched.
        Assert.Equal(2, await ctx.VpnServers.CountAsync(s => !s.IsDeleted));
    }

    [Fact]
    public async Task AnnounceAsync_AllowsPending_WhenOnlySoftDeletedServerHasSameUrl()
    {
        await using var ctx = CreateContext();
        SeedServer(ctx, id: 5, name: "gone", apiUrl: "http://10.0.0.1:5010/", deleted: true);
        SeedServer(ctx, id: 6, name: "alive", apiUrl: "http://10.0.0.2:5010/", deleted: false);
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx, out var discoveryCmd, out var notifications, out _);
        WireCommandToContext(ctx, discoveryCmd);

        var result = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = "http://10.0.0.1:5010/",
            SuggestedName = "rejoined"
        }, CancellationToken.None);

        Assert.Equal(AnnounceVpnServerResultStatus.Pending, result.Status);
        Assert.NotNull(result.DiscoveryId);
        notifications.Verify(
            n => n.NotifyDiscovered(It.IsAny<int>(), "rejoined", "http://10.0.0.1:5010/", It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.True(await ctx.VpnServers.AnyAsync(s => s.Id == 6 && !s.IsDeleted));
    }

    [Fact]
    public async Task AnnounceAsync_CreatesPending_AndNotifies_AlongsideExistingFleet()
    {
        await using var ctx = CreateContext();
        SeedServer(ctx, id: 1, name: "fleet-a", apiUrl: "http://10.0.0.10:5010/", deleted: false);
        SeedServer(ctx, id: 2, name: "fleet-b", apiUrl: "http://10.0.0.11:5010/", deleted: false);
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx, out var discoveryCmd, out var notifications, out _);
        WireCommandToContext(ctx, discoveryCmd);

        var result = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.Xray,
            ApiUrl = "http://203.0.113.10:5011/",
            SuggestedName = "xray-1",
            PublicIp = "203.0.113.10",
            Version = "1.2.3",
            IsEnableWss = true
        }, CancellationToken.None);

        Assert.Equal(AnnounceVpnServerResultStatus.Pending, result.Status);
        Assert.Equal(result.DiscoveryId, (await ctx.VpnServerDiscoveries.SingleAsync()).Id);

        var added = await ctx.VpnServerDiscoveries.SingleAsync();
        Assert.Equal(VpnServerDiscoveryStatus.Pending, added.Status);
        Assert.Equal(VpnServerType.Xray, added.ServerType);
        Assert.Equal("http://203.0.113.10:5011/", added.ApiUrl);
        Assert.Equal("xray-1", added.SuggestedName);
        Assert.Equal("203.0.113.10", added.PublicIp);
        Assert.Equal("1.2.3", added.Version);
        Assert.True(added.IsEnableWss);
        Assert.NotNull(added.LastNotifiedUtc);

        notifications.Verify(
            n => n.NotifyDiscovered(added.Id, "xray-1", "http://203.0.113.10:5011/", It.IsAny<CancellationToken>()),
            Times.Once);

        // Fleet unchanged until admin approves.
        Assert.Equal(2, await ctx.VpnServers.CountAsync(s => !s.IsDeleted));
        Assert.False(await ctx.VpnServers.AnyAsync(s => s.ApiUrl == "http://203.0.113.10:5011/"));
    }

    [Fact]
    public async Task AnnounceAsync_ReturnsRejected_AndDoesNotReopen()
    {
        await using var ctx = CreateContext();
        var now = DateTimeOffset.UtcNow;
        ctx.VpnServerDiscoveries.Add(new VpnServerDiscovery
        {
            Id = 9,
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = "http://10.0.0.8:5010/",
            SuggestedName = "denied-node",
            Status = VpnServerDiscoveryStatus.Rejected,
            RejectedAt = now.AddDays(-1),
            RejectReason = "spam",
            LastSeenUtc = now.AddDays(-1),
            CreateDate = now.AddDays(-2),
            LastUpdate = now.AddDays(-1)
        });
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx, out var discoveryCmd, out var notifications, out _);

        var result = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = "http://10.0.0.8:5010/",
            SuggestedName = "try-again"
        }, CancellationToken.None);

        Assert.Equal(AnnounceVpnServerResultStatus.Rejected, result.Status);
        Assert.Equal(9, result.DiscoveryId);
        Assert.Equal(1, await ctx.VpnServerDiscoveries.CountAsync());
        Assert.Equal(VpnServerDiscoveryStatus.Rejected, (await ctx.VpnServerDiscoveries.SingleAsync()).Status);
        discoveryCmd.Verify(c => c.Add(It.IsAny<VpnServerDiscovery>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        notifications.Verify(
            n => n.NotifyDiscovered(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AnnounceAsync_RefreshesExistingPending_WithoutImmediateRenotify()
    {
        await using var ctx = CreateContext();
        var now = DateTimeOffset.UtcNow;
        ctx.VpnServerDiscoveries.Add(new VpnServerDiscovery
        {
            Id = 7,
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = "http://10.0.0.2:5010/",
            SuggestedName = "old",
            Status = VpnServerDiscoveryStatus.Pending,
            LastSeenUtc = now.AddHours(-1),
            LastNotifiedUtc = now.AddMinutes(-5),
            CreateDate = now.AddHours(-2),
            LastUpdate = now.AddHours(-1)
        });
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx, out var discoveryCmd, out var notifications, out _);
        WireCommandToContext(ctx, discoveryCmd);

        var result = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = "http://10.0.0.2:5010/",
            SuggestedName = "new-name",
            Version = "9.9.9"
        }, CancellationToken.None);

        Assert.Equal(AnnounceVpnServerResultStatus.Pending, result.Status);
        Assert.Equal(7, result.DiscoveryId);
        var row = await ctx.VpnServerDiscoveries.FindAsync(7);
        Assert.Equal("new-name", row!.SuggestedName);
        Assert.Equal("9.9.9", row.Version);
        Assert.True(row.LastSeenUtc > now.AddMinutes(-1));
        notifications.Verify(
            n => n.NotifyDiscovered(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AnnounceAsync_Renotifies_WhenCooldownElapsed()
    {
        await using var ctx = CreateContext();
        var now = DateTimeOffset.UtcNow;
        ctx.VpnServerDiscoveries.Add(new VpnServerDiscovery
        {
            Id = 8,
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = "http://10.0.0.3:5010/",
            SuggestedName = "stale",
            Status = VpnServerDiscoveryStatus.Pending,
            LastSeenUtc = now.AddDays(-2),
            LastNotifiedUtc = now.AddHours(-25),
            CreateDate = now.AddDays(-3),
            LastUpdate = now.AddDays(-2)
        });
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx, out var discoveryCmd, out var notifications, out _);
        WireCommandToContext(ctx, discoveryCmd);

        var result = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = "http://10.0.0.3:5010/",
            SuggestedName = "stale"
        }, CancellationToken.None);

        Assert.Equal(AnnounceVpnServerResultStatus.Pending, result.Status);
        notifications.Verify(
            n => n.NotifyDiscovered(8, "stale", "http://10.0.0.3:5010/", It.IsAny<CancellationToken>()),
            Times.Once);
        var row = await ctx.VpnServerDiscoveries.FindAsync(8);
        Assert.NotNull(row!.LastNotifiedUtc);
        Assert.True(row.LastNotifiedUtc > now.AddHours(-1));
    }

    [Fact]
    public async Task ListPendingAsync_ReturnsOnlyPending_OrderedByLastSeenDesc()
    {
        await using var ctx = CreateContext();
        var now = DateTimeOffset.UtcNow;
        ctx.VpnServerDiscoveries.AddRange(
            new VpnServerDiscovery
            {
                Id = 1, ApiUrl = "http://a/", SuggestedName = "old-pending",
                Status = VpnServerDiscoveryStatus.Pending, LastSeenUtc = now.AddHours(-2),
                CreateDate = now, LastUpdate = now
            },
            new VpnServerDiscovery
            {
                Id = 2, ApiUrl = "http://b/", SuggestedName = "new-pending",
                Status = VpnServerDiscoveryStatus.Pending, LastSeenUtc = now.AddMinutes(-5),
                CreateDate = now, LastUpdate = now
            },
            new VpnServerDiscovery
            {
                Id = 3, ApiUrl = "http://c/", SuggestedName = "approved",
                Status = VpnServerDiscoveryStatus.Approved, LastSeenUtc = now,
                CreateDate = now, LastUpdate = now
            },
            new VpnServerDiscovery
            {
                Id = 4, ApiUrl = "http://d/", SuggestedName = "rejected",
                Status = VpnServerDiscoveryStatus.Rejected, LastSeenUtc = now,
                CreateDate = now, LastUpdate = now
            });
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx, out _, out _, out _);
        var list = await sut.ListPendingAsync(CancellationToken.None);

        Assert.Equal(2, list.Discoveries.Count);
        Assert.Equal(2, list.Discoveries[0].Id);
        Assert.Equal(1, list.Discoveries[1].Id);
        Assert.All(list.Discoveries, d => Assert.Equal(VpnServerDiscoveryStatus.Pending, d.Status));
    }

    [Fact]
    public async Task ApproveAsync_CreatesVpnServer_ViaAddVpnServer_AndLeavesFleetIntact()
    {
        await using var ctx = CreateContext();
        SeedServer(ctx, id: 1, name: "fleet-a", apiUrl: "http://10.0.0.10:5010/", deleted: false);
        SeedServer(ctx, id: 2, name: "fleet-b", apiUrl: "http://10.0.0.11:5010/", deleted: false);
        ctx.VpnServerDiscoveries.Add(new VpnServerDiscovery
        {
            Id = 11,
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = "http://10.0.0.50:5010/",
            SuggestedName = "node-b",
            IsEnableWss = true,
            Status = VpnServerDiscoveryStatus.Pending,
            LastSeenUtc = DateTimeOffset.UtcNow,
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow
        });
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx, out var discoveryCmd, out _, out var vpnData);
        WireCommandToContext(ctx, discoveryCmd);
        vpnData
            .Setup(v => v.AddVpnServer(It.IsAny<VpnServer>(), It.IsAny<List<int>>(), It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServer s, List<int> plans, List<int> tags, CancellationToken _) =>
            {
                s.Id = 99;
                // Simulate real AddVpnServer side-effect: persist new row (fleet grows).
                ctx.VpnServers.Add(s);
                ctx.SaveChanges();
                Assert.Equal(new[] { 3, 7 }, plans);
                Assert.Equal(new[] { 9 }, tags);
                return s;
            });

        var result = await sut.ApproveAsync(11, new ApproveVpnServerDiscoveryRequest
        {
            QuotaPlanIds = [3, 7],
            TagIds = [9],
            IsDefault = false
        }, CancellationToken.None);

        Assert.Equal(99, result.VpnServerId);
        Assert.Equal(VpnServerDiscoveryStatus.Approved, result.Discovery.Status);
        Assert.Equal(99, result.Discovery.ResolvedVpnServerId);
        vpnData.Verify(v => v.AddVpnServer(
            It.Is<VpnServer>(s =>
                s.ServerName == "node-b"
                && s.ApiUrl == "http://10.0.0.50:5010/"
                && s.IsEnableWss
                && s.ServerType == VpnServerType.OpenVpn),
            It.IsAny<List<int>>(),
            It.IsAny<List<int>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Existing servers still there; new one added through AddVpnServer.
        Assert.Equal(3, await ctx.VpnServers.CountAsync(s => !s.IsDeleted));
        Assert.True(await ctx.VpnServers.AnyAsync(s => s.Id == 1));
        Assert.True(await ctx.VpnServers.AnyAsync(s => s.Id == 2));
        Assert.True(await ctx.VpnServers.AnyAsync(s => s.Id == 99));
    }

    [Fact]
    public async Task ApproveAsync_ThenAnnounceSameUrl_ReturnsAlreadyRegistered()
    {
        await using var ctx = CreateContext();
        SeedServer(ctx, id: 1, name: "fleet-a", apiUrl: "http://10.0.0.10:5010/", deleted: false);
        ctx.VpnServerDiscoveries.Add(new VpnServerDiscovery
        {
            Id = 20,
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = "http://10.0.0.55:5010/",
            SuggestedName = "newbie",
            Status = VpnServerDiscoveryStatus.Pending,
            LastSeenUtc = DateTimeOffset.UtcNow,
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow
        });
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx, out var discoveryCmd, out var notifications, out var vpnData);
        WireCommandToContext(ctx, discoveryCmd);
        vpnData
            .Setup(v => v.AddVpnServer(It.IsAny<VpnServer>(), It.IsAny<List<int>>(), It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServer s, List<int> _, List<int> _, CancellationToken _) =>
            {
                s.Id = 77;
                ctx.VpnServers.Add(s);
                ctx.SaveChanges();
                return s;
            });

        var approved = await sut.ApproveAsync(20, new ApproveVpnServerDiscoveryRequest(), CancellationToken.None);
        Assert.Equal(77, approved.VpnServerId);

        var again = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = "http://10.0.0.55:5010/"
        }, CancellationToken.None);

        Assert.Equal(AnnounceVpnServerResultStatus.AlreadyRegistered, again.Status);
        Assert.Equal(77, again.ExistingVpnServerId);
        notifications.Verify(
            n => n.NotifyDiscovered(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ApproveAsync_UsesServerNameOverride_AndFallsBackToHost()
    {
        await using var ctx = CreateContext();
        ctx.VpnServerDiscoveries.Add(new VpnServerDiscovery
        {
            Id = 21,
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = "http://edge.example.com:5010/",
            SuggestedName = null,
            Status = VpnServerDiscoveryStatus.Pending,
            LastSeenUtc = DateTimeOffset.UtcNow,
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow
        });
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx, out var discoveryCmd, out _, out var vpnData);
        WireCommandToContext(ctx, discoveryCmd);

        VpnServer? captured = null;
        vpnData
            .Setup(v => v.AddVpnServer(It.IsAny<VpnServer>(), It.IsAny<List<int>>(), It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServer s, List<int> _, List<int> _, CancellationToken _) =>
            {
                captured = s;
                s.Id = 1;
                return s;
            });

        await sut.ApproveAsync(21, new ApproveVpnServerDiscoveryRequest(), CancellationToken.None);
        Assert.Equal("edge.example.com", captured!.ServerName);

        // Reset discovery to pending for override path
        var d = await ctx.VpnServerDiscoveries.FindAsync(21);
        d!.Status = VpnServerDiscoveryStatus.Pending;
        d.ResolvedVpnServerId = null;
        await ctx.SaveChangesAsync();

        await sut.ApproveAsync(21, new ApproveVpnServerDiscoveryRequest { ServerName = "  custom-name  " }, CancellationToken.None);
        Assert.Equal("custom-name", captured.ServerName);
    }

    [Fact]
    public async Task ApproveAsync_Throws_WhenNotFound_OrNotPending()
    {
        await using var ctx = CreateContext();
        ctx.VpnServerDiscoveries.Add(new VpnServerDiscovery
        {
            Id = 30,
            ApiUrl = "http://x/",
            Status = VpnServerDiscoveryStatus.Approved,
            LastSeenUtc = DateTimeOffset.UtcNow,
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow
        });
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx, out _, out _, out _);

        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ApproveAsync(999, new ApproveVpnServerDiscoveryRequest(), CancellationToken.None));
        Assert.Equal(VpnServerDiscoveryService.NotFoundMessage, missing.Message);

        var notPending = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ApproveAsync(30, new ApproveVpnServerDiscoveryRequest(), CancellationToken.None));
        Assert.Equal(VpnServerDiscoveryService.NotPendingMessage, notPending.Message);
    }

    [Fact]
    public async Task DenyAsync_MarksRejected_WithReason()
    {
        await using var ctx = CreateContext();
        ctx.VpnServerDiscoveries.Add(new VpnServerDiscovery
        {
            Id = 12,
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = "http://10.0.0.4:5010/",
            SuggestedName = "node-c",
            Status = VpnServerDiscoveryStatus.Pending,
            LastSeenUtc = DateTimeOffset.UtcNow,
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow
        });
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx, out var discoveryCmd, out _, out _);
        WireCommandToContext(ctx, discoveryCmd);

        var result = await sut.DenyAsync(12, new DenyVpnServerDiscoveryRequest { Reason = "not ours" }, CancellationToken.None);

        Assert.Equal(VpnServerDiscoveryStatus.Rejected, result.Discovery.Status);
        Assert.Equal(12, result.Discovery.Id);
        var row = await ctx.VpnServerDiscoveries.FindAsync(12);
        Assert.Equal(VpnServerDiscoveryStatus.Rejected, row!.Status);
        Assert.Equal("not ours", row.RejectReason);
        Assert.NotNull(row.RejectedAt);
    }

    [Fact]
    public async Task DenyAsync_Throws_WhenNotFound_OrNotPending()
    {
        await using var ctx = CreateContext();
        ctx.VpnServerDiscoveries.Add(new VpnServerDiscovery
        {
            Id = 40,
            ApiUrl = "http://y/",
            Status = VpnServerDiscoveryStatus.Rejected,
            LastSeenUtc = DateTimeOffset.UtcNow,
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow
        });
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx, out _, out _, out _);

        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.DenyAsync(999, null, CancellationToken.None));
        Assert.Equal(VpnServerDiscoveryService.NotFoundMessage, missing.Message);

        var notPending = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.DenyAsync(40, null, CancellationToken.None));
        Assert.Equal(VpnServerDiscoveryService.NotPendingMessage, notPending.Message);
    }

    [Fact]
    public void TryAcquireAnnounceSlot_RateLimitsPerIp()
    {
        using var ctx = CreateContext();
        var sut = CreateSut(ctx, out _, out _, out _);

        for (var i = 0; i < 30; i++)
            Assert.True(sut.TryAcquireAnnounceSlot("1.2.3.4"));

        Assert.False(sut.TryAcquireAnnounceSlot("1.2.3.4"));
        Assert.True(sut.TryAcquireAnnounceSlot("9.9.9.9"));
        Assert.True(sut.TryAcquireAnnounceSlot(null));
    }

    private static void SeedServer(ApplicationDbContext ctx, int id, string name, string apiUrl, bool deleted)
    {
        var now = DateTimeOffset.UtcNow;
        ctx.VpnServers.Add(new VpnServer
        {
            Id = id,
            ServerName = name,
            ApiUrl = apiUrl,
            IsDeleted = deleted,
            CreateDate = now,
            LastUpdate = now
        });
    }

    private static void WireCommandToContext(
        ApplicationDbContext ctx,
        Mock<ICommandService<VpnServerDiscovery, int>> discoveryCmd)
    {
        discoveryCmd
            .Setup(c => c.Add(It.IsAny<VpnServerDiscovery>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServerDiscovery d, bool _, CancellationToken _) =>
            {
                if (d.Id == 0)
                {
                    var next = ctx.VpnServerDiscoveries.AsEnumerable().Select(x => x.Id).DefaultIfEmpty(0).Max() + 1;
                    d.Id = next;
                }

                ctx.VpnServerDiscoveries.Add(d);
                ctx.SaveChanges();
                return d;
            });
        discoveryCmd
            .Setup(c => c.Update(It.IsAny<VpnServerDiscovery>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServerDiscovery d, bool _, CancellationToken _) =>
            {
                ctx.VpnServerDiscoveries.Update(d);
                return ctx.SaveChanges();
            });
    }

    private static VpnServerDiscoveryService CreateSut(
        ApplicationDbContext ctx,
        out Mock<ICommandService<VpnServerDiscovery, int>> discoveryCmd,
        out Mock<IServerOpenVpnNotificationService> notifications,
        out Mock<IVpnDataService> vpnData)
    {
        var uow = new DbContextUnitOfWork(ctx);
        var discoveryQuery = new EfQueryService<VpnServerDiscovery, int>(uow);
        var vpnServerQuery = new EfQueryService<VpnServer, int>(uow);
        discoveryCmd = new Mock<ICommandService<VpnServerDiscovery, int>>(MockBehavior.Strict);
        notifications = new Mock<IServerOpenVpnNotificationService>(MockBehavior.Loose);
        vpnData = new Mock<IVpnDataService>(MockBehavior.Strict);
        var cache = new MemoryCache(new MemoryCacheOptions());

        return new VpnServerDiscoveryService(
            discoveryQuery,
            discoveryCmd.Object,
            vpnServerQuery,
            vpnData.Object,
            notifications.Object,
            cache,
            NullLogger<VpnServerDiscoveryService>.Instance);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataBaseSettings:DefaultSchema"] = "test_schema"
            })
            .Build();
        return new ApplicationDbContext(options, configuration);
    }

    private sealed class DbContextUnitOfWork(ApplicationDbContext ctx) : IUnitOfWork
    {
        public void Dispose() { }

        public IRepository<T> GetRepository<T>() where T : class
            => throw new NotImplementedException();

        public IQuery<T> GetQuery<T>() where T : class
            => new TestQuery<T>(ctx.Set<T>());

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => ctx.SaveChangesAsync(cancellationToken);
        public void SaveChanges() => ctx.SaveChanges();
        public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
            => throw new NotImplementedException();
        public void MarkPropertyModified<T>(T entity, System.Linq.Expressions.Expression<Func<T, object>> property) where T : class
            => throw new NotImplementedException();
    }
}
