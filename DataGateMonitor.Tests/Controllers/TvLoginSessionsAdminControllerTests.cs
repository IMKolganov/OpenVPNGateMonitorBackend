using DataGateMonitor.Controllers;
using DataGateMonitor.Services.Api.Auth.TvLogin;
using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Responses;
using DataGateMonitor.SharedModels.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace DataGateMonitor.Tests.Controllers;

public class TvLoginSessionsAdminControllerTests
{
    [Fact]
    public async Task List_ReturnsSessionsFromService()
    {
        var sessionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var admin = new Mock<ITvLoginAdminService>();
        admin.Setup(s => s.ListAsync(99, "approved", 5, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetAdminTvLoginSessionsResponse
            {
                TotalCount = 1,
                Sessions =
                [
                    new AdminTvLoginSessionDto
                    {
                        SessionId = sessionId,
                        UserCode = "123456",
                        Status = "approved",
                        ApprovedUserId = 99
                    }
                ]
            });

        var controller = new TvLoginSessionsAdminController(admin.Object);
        var result = await controller.List(99, "approved", 5, 25, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<GetAdminTvLoginSessionsResponse>>(ok.Value);
        Assert.True(payload.Success);
        Assert.Equal(1, payload.Data!.TotalCount);
        Assert.Equal(sessionId, Assert.Single(payload.Data.Sessions).SessionId);
        admin.Verify(s => s.ListAsync(99, "approved", 5, 25, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUserSummary_ReturnsServicePayload()
    {
        var admin = new Mock<ITvLoginAdminService>();
        admin.Setup(s => s.GetUserSummaryAsync(15, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserTvLoginSummaryResponse
            {
                HasUsedTvLogin = true,
                ApprovedOrConsumedCount = 3,
                LastDeviceName = "Living Room TV",
                LastClient = "android-tv"
            });

        var controller = new TvLoginSessionsAdminController(admin.Object);
        var result = await controller.GetUserSummary(15, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<UserTvLoginSummaryResponse>>(ok.Value);
        Assert.True(payload.Success);
        Assert.True(payload.Data!.HasUsedTvLogin);
        Assert.Equal(3, payload.Data.ApprovedOrConsumedCount);
        Assert.Equal("Living Room TV", payload.Data.LastDeviceName);
        admin.Verify(s => s.GetUserSummaryAsync(15, It.IsAny<CancellationToken>()), Times.Once);
    }
}
