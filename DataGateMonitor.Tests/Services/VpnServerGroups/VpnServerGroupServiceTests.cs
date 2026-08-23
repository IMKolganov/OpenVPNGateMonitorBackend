using Moq;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query;
using DataGateMonitor.DataBase.Services.Query.VpnServerGroupTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.VpnServerGroups;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerGroups.Requests;
using Xunit;

namespace DataGateMonitor.Tests.Services.VpnServerGroups;

public class VpnServerGroupServiceTests
{
    private readonly Mock<ICommandService<VpnServerGroup, int>> _groupCommand = new();
    private readonly Mock<IVpnServerGroupQueryService> _groupQuery = new();
    private readonly Mock<IQueryService<VpnServer, int>> _serverQuery = new();
    private readonly Mock<ICommandService<VpnServer, int>> _serverCommand = new();
    private readonly Mock<ITransactionRunner> _tx = new();

    public VpnServerGroupServiceTests()
    {
        _tx.Setup(t => t.RunAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>(async (action, ct) => await action(ct));
    }

    private VpnServerGroupService CreateService() => new(
        _groupCommand.Object,
        _groupQuery.Object,
        _serverQuery.Object,
        _serverCommand.Object,
        _tx.Object);

    [Fact]
    public async Task GetAllAsync_ReturnsGroupsWithOrderedServerIds()
    {
        _groupQuery.Setup(q => q.GetAll(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new VpnServerGroup { Id = 1, Name = "EU", SortOrder = 0 },
                new VpnServerGroup { Id = 2, Name = "US", SortOrder = 1 },
            ]);
        _serverQuery.Setup(q => q.Where(
                It.IsAny<System.Linq.Expressions.Expression<Func<VpnServer, bool>>>(),
                It.IsAny<Func<IQueryable<VpnServer>, IOrderedQueryable<VpnServer>>>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new VpnServer { Id = 11, VpnServerGroupId = 1, SortOrder = 0 },
                new VpnServer { Id = 10, VpnServerGroupId = 1, SortOrder = 1 },
                new VpnServer { Id = 20, VpnServerGroupId = 2, SortOrder = 0 },
            ]);

        var result = await CreateService().GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal([11, 10], result[0].ServerIds);
        Assert.Equal([20], result[1].ServerIds);
    }

    [Fact]
    public async Task CreateAsync_RejectsBlankName()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateAsync("  "));
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateName()
    {
        _groupQuery.Setup(q => q.GetByName("EU", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServerGroup { Id = 1, Name = "EU" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateAsync(" EU "));
    }

    [Fact]
    public async Task CreateAsync_AppendsSortOrder()
    {
        _groupQuery.Setup(q => q.GetByName("Asia", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServerGroup?)null);
        _groupQuery.Setup(q => q.GetAll(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new VpnServerGroup { Id = 1, Name = "EU", SortOrder = 3 }]);
        VpnServerGroup? captured = null;
        _groupCommand.Setup(c => c.Add(It.IsAny<VpnServerGroup>(), true, It.IsAny<CancellationToken>()))
            .Callback<VpnServerGroup, bool, CancellationToken>((g, _, _) => captured = g)
            .ReturnsAsync((VpnServerGroup g, bool _, CancellationToken _) =>
            {
                g.Id = 9;
                return g;
            });

        var dto = await CreateService().CreateAsync(" Asia ");

        Assert.Equal(9, dto.Id);
        Assert.Equal("Asia", captured!.Name);
        Assert.Equal(4, captured.SortOrder);
    }

    [Fact]
    public async Task DeleteAsync_UnassignsMembersThenDeletes()
    {
        _groupQuery.Setup(q => q.GetById(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServerGroup { Id = 5, Name = "EU" });
        var member = new VpnServer { Id = 10, VpnServerGroupId = 5 };
        _serverQuery.Setup(q => q.Where(
                It.IsAny<System.Linq.Expressions.Expression<Func<VpnServer, bool>>>(),
                null,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([member]);
        _serverCommand.Setup(c => c.Update(It.IsAny<VpnServer>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _serverCommand.Setup(c => c.SaveChanges(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _groupCommand.Setup(c => c.DeleteById(5, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await CreateService().DeleteAsync(5);

        Assert.Null(member.VpnServerGroupId);
        _groupCommand.Verify(c => c.DeleteById(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReorderAsync_UpdatesSortOrders()
    {
        var g1 = new VpnServerGroup { Id = 1, Name = "A", SortOrder = 0 };
        var g2 = new VpnServerGroup { Id = 2, Name = "B", SortOrder = 1 };
        _groupQuery.Setup(q => q.GetById(1, It.IsAny<CancellationToken>())).ReturnsAsync(g1);
        _groupQuery.Setup(q => q.GetById(2, It.IsAny<CancellationToken>())).ReturnsAsync(g2);
        _groupCommand.Setup(c => c.Update(It.IsAny<VpnServerGroup>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _groupCommand.Setup(c => c.SaveChanges(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await CreateService().ReorderAsync(new ReorderVpnServerGroupsRequest
        {
            Items =
            [
                new VpnServerGroupOrderItem { GroupId = 2, SortOrder = 0 },
                new VpnServerGroupOrderItem { GroupId = 1, SortOrder = 1 },
            ],
        });

        Assert.Equal(1, g1.SortOrder);
        Assert.Equal(0, g2.SortOrder);
    }

    [Fact]
    public async Task SetServersAsync_MovesMembershipAndOrder()
    {
        _groupQuery.Setup(q => q.GetById(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServerGroup { Id = 1, Name = "EU" });

        var previous = new VpnServer { Id = 99, VpnServerGroupId = 1, SortOrder = 0 };
        var a = new VpnServer { Id = 10, VpnServerGroupId = null, SortOrder = 0 };
        var b = new VpnServer { Id = 11, VpnServerGroupId = 2, SortOrder = 0 };

        _serverQuery.Setup(q => q.Where(
                It.IsAny<System.Linq.Expressions.Expression<Func<VpnServer, bool>>>(),
                null,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([previous]);
        _serverQuery.Setup(q => q.FindById(10, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(a);
        _serverQuery.Setup(q => q.FindById(11, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(b);
        _serverCommand.Setup(c => c.Update(It.IsAny<VpnServer>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _serverCommand.Setup(c => c.SaveChanges(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var dto = await CreateService().SetServersAsync(1, new SetVpnServerGroupServersRequest
        {
            VpnServerIds = [11, 10],
        });

        Assert.Null(previous.VpnServerGroupId);
        Assert.Equal(1, a.VpnServerGroupId);
        Assert.Equal(1, a.SortOrder);
        Assert.Equal(1, b.VpnServerGroupId);
        Assert.Equal(0, b.SortOrder);
        Assert.Equal([11, 10], dto.ServerIds);
    }

    [Fact]
    public async Task SetUngroupedServersAsync_OnlyUpdatesListedServers()
    {
        var s1 = new VpnServer { Id = 1, VpnServerGroupId = 5, SortOrder = 9 };
        var s2 = new VpnServer { Id = 2, VpnServerGroupId = null, SortOrder = 3 };
        _serverQuery.Setup(q => q.FindById(1, false, It.IsAny<CancellationToken>())).ReturnsAsync(s1);
        _serverQuery.Setup(q => q.FindById(2, false, It.IsAny<CancellationToken>())).ReturnsAsync(s2);
        _serverCommand.Setup(c => c.Update(It.IsAny<VpnServer>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _serverCommand.Setup(c => c.SaveChanges(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await CreateService().SetUngroupedServersAsync(new SetVpnServerGroupServersRequest
        {
            VpnServerIds = [2, 1],
        });

        Assert.Null(s1.VpnServerGroupId);
        Assert.Equal(1, s1.SortOrder);
        Assert.Null(s2.VpnServerGroupId);
        Assert.Equal(0, s2.SortOrder);
    }
}
