using DataGateMonitor.Controllers;
using DataGateMonitor.Services.Performance;
using DataGateMonitor.SharedModels.DataGateMonitor.Performance;
using DataGateMonitor.SharedModels.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace DataGateMonitor.Tests.Controllers;

public class PerformanceControllerTests
{
    [Fact]
    public async Task GetHttpRequests_Maps_Store_Samples()
    {
        var store = new Mock<IPerformanceSampleStore>();
        store.Setup(s => s.GetHttpLatestAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PerformanceHttpSample
                {
                    TimestampUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                    RequestId = "abc",
                    Method = "GET",
                    Path = "/api/x",
                    StatusCode = 200,
                    DurationMs = 250,
                    UserName = "admin",
                    TraceId = "t1"
                }
            ]);

        var controller = new PerformanceController(store.Object);
        var result = await controller.GetHttpRequests(50, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<PerformanceHttpRequestsResponse>>(ok.Value);
        var item = Assert.Single(payload.Data!.Items);
        Assert.Equal("abc", item.RequestId);
        Assert.Equal(250, item.DurationMs);
        Assert.Equal("/api/x", item.Path);
        Assert.Equal("admin", item.UserName);
        Assert.Equal("t1", item.TraceId);
    }

    [Fact]
    public async Task GetDbQueries_Maps_Store_Samples()
    {
        var store = new Mock<IPerformanceSampleStore>();
        store.Setup(s => s.GetDbLatestAsync(25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PerformanceDbSample
                {
                    TimestampUtc = DateTimeOffset.Parse("2026-01-02T00:00:00Z"),
                    RequestId = "req-db",
                    DurationMs = 180,
                    CommandType = "Text",
                    Sql = "SELECT 1",
                    Succeeded = false,
                    ExceptionMessage = "timeout"
                }
            ]);

        var controller = new PerformanceController(store.Object);
        var result = await controller.GetDbQueries(25, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<PerformanceDbQueriesResponse>>(ok.Value);
        var item = Assert.Single(payload.Data!.Items);
        Assert.Equal("req-db", item.RequestId);
        Assert.Equal(180, item.DurationMs);
        Assert.Equal("SELECT 1", item.Sql);
        Assert.False(item.Succeeded);
        Assert.Equal("timeout", item.ExceptionMessage);
    }

    [Fact]
    public async Task ClearAll_Delegates_To_Store()
    {
        var store = new Mock<IPerformanceSampleStore>();
        store.Setup(s => s.ClearAllAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var controller = new PerformanceController(store.Object);

        var result = await controller.ClearAll(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        store.Verify(s => s.ClearAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClearHttp_Delegates_To_Store()
    {
        var store = new Mock<IPerformanceSampleStore>();
        store.Setup(s => s.ClearHttpAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var controller = new PerformanceController(store.Object);

        var result = await controller.ClearHttp(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        store.Verify(s => s.ClearHttpAsync(It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(s => s.ClearDbAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ClearDb_Delegates_To_Store()
    {
        var store = new Mock<IPerformanceSampleStore>();
        store.Setup(s => s.ClearDbAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var controller = new PerformanceController(store.Object);

        var result = await controller.ClearDb(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        store.Verify(s => s.ClearDbAsync(It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(s => s.ClearHttpAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
