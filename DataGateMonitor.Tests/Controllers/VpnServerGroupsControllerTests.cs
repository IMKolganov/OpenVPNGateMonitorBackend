using Mapster;
using Microsoft.AspNetCore.Mvc;
using Moq;
using DataGateMonitor.Controllers;
using DataGateMonitor.Mapping.VpnServerGroups.Mappings;
using DataGateMonitor.Services.VpnServerGroups;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerGroups.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerGroups.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerGroups.Responses;
using DataGateMonitor.SharedModels.Responses;
using Xunit;

namespace DataGateMonitor.Tests.Controllers;

public class VpnServerGroupsControllerTests
{
    private readonly Mock<IVpnServerGroupService> _service = new();
    private readonly VpnServerGroupsController _controller;

    public VpnServerGroupsControllerTests()
    {
        TypeAdapterConfig.GlobalSettings.Scan(typeof(VpnServerGroupMapping).Assembly);
        _controller = new VpnServerGroupsController(_service.Object);
    }

    [Fact]
    public async Task GetAll_Returns_Ok_WithGroups()
    {
        var groups = new List<VpnServerGroupDto>
        {
            new() { Id = 1, Name = "EU", SortOrder = 0, ServerIds = [10] },
            new() { Id = 2, Name = "US", SortOrder = 1 },
        };
        _service.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(groups);

        var result = await _controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<VpnServerGroupsResponse>>(ok.Value);
        Assert.True(response.Success);
        Assert.Equal(2, response.Data!.Groups.Count);
        Assert.Equal("EU", response.Data.Groups[0].Name);
        _service.Verify(s => s.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetById_WhenFound_Returns_Ok()
    {
        var group = new VpnServerGroupDto { Id = 5, Name = "Nordics", SortOrder = 2, ServerIds = [1, 2] };
        _service.Setup(s => s.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(group);

        var result = await _controller.GetById(5, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<VpnServerGroupResponse>>(ok.Value);
        Assert.True(response.Success);
        Assert.Equal(5, response.Data!.Group.Id);
        Assert.Equal(new[] { 1, 2 }, response.Data.Group.ServerIds);
    }

    [Fact]
    public async Task GetById_WhenNotFound_Returns_NotFound()
    {
        _service.Setup(s => s.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((VpnServerGroupDto?)null);

        var result = await _controller.GetById(99, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_Returns_Ok()
    {
        var created = new VpnServerGroupDto { Id = 10, Name = "Asia", SortOrder = 3 };
        _service.Setup(s => s.CreateAsync("Asia", It.IsAny<CancellationToken>())).ReturnsAsync(created);

        var result = await _controller.Create(new CreateOrUpdateVpnServerGroupRequest { Name = "Asia" }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<VpnServerGroupResponse>>(ok.Value);
        Assert.Equal(10, response.Data!.Group.Id);
    }

    [Fact]
    public async Task Update_Returns_Ok()
    {
        var updated = new VpnServerGroupDto { Id = 3, Name = "Updated", SortOrder = 0 };
        _service.Setup(s => s.UpdateAsync(3, "Updated", It.IsAny<CancellationToken>())).ReturnsAsync(updated);

        var result = await _controller.Update(3, new CreateOrUpdateVpnServerGroupRequest { Name = "Updated" }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("Updated", Assert.IsType<ApiResponse<VpnServerGroupResponse>>(ok.Value).Data!.Group.Name);
    }

    [Fact]
    public async Task Delete_Returns_Ok()
    {
        _service.Setup(s => s.DeleteAsync(7, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _controller.Delete(7, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(Assert.IsType<ApiResponse<bool>>(ok.Value).Data);
    }

    [Fact]
    public async Task Reorder_Returns_Ok()
    {
        _service.Setup(s => s.ReorderAsync(It.IsAny<ReorderVpnServerGroupsRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.Reorder(
            new ReorderVpnServerGroupsRequest
            {
                Items = [new VpnServerGroupOrderItem { GroupId = 1, SortOrder = 0 }],
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(Assert.IsType<ApiResponse<bool>>(ok.Value).Data);
    }

    [Fact]
    public async Task SetServers_Returns_Ok()
    {
        var group = new VpnServerGroupDto { Id = 1, Name = "EU", ServerIds = [2, 3] };
        _service.Setup(s => s.SetServersAsync(1, It.IsAny<SetVpnServerGroupServersRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var result = await _controller.SetServers(
            1,
            new SetVpnServerGroupServersRequest { VpnServerIds = [2, 3] },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(new[] { 2, 3 }, Assert.IsType<ApiResponse<VpnServerGroupResponse>>(ok.Value).Data!.Group.ServerIds);
    }

    [Fact]
    public async Task SetUngroupedServers_Returns_Ok()
    {
        _service.Setup(s => s.SetUngroupedServersAsync(It.IsAny<SetVpnServerGroupServersRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.SetUngroupedServers(
            new SetVpnServerGroupServersRequest { VpnServerIds = [9] },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(Assert.IsType<ApiResponse<bool>>(ok.Value).Data);
    }
}
