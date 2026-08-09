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
}
