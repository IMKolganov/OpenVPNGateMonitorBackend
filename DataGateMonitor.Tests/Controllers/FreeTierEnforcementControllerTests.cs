using DataGateMonitor.Controllers;
using DataGateMonitor.Services.Users.Interfaces;
using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Responses;
using DataGateMonitor.SharedModels.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace DataGateMonitor.Tests.Controllers;

public class FreeTierEnforcementControllerTests
{
    [Fact]
    public async Task GetCandidates_ReturnsServicePayload()
    {
        var overview = new Mock<IFreeTierEnforcementOverviewService>();
        overview.Setup(s => s.GetCandidatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetFreeTierEnforcementCandidatesResponse
            {
                TotalCount = 1,
                ConnectedCount = 1,
                Candidates =
                [
                    new FreeTierEnforcementCandidateDto
                    {
                        UserId = 42,
                        DisplayName = "free-user",
                        IsConnected = true,
                        VpnServerId = 7
                    }
                ]
            });

        var controller = new FreeTierEnforcementController(overview.Object);
        var result = await controller.GetCandidates(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<GetFreeTierEnforcementCandidatesResponse>>(ok.Value);
        Assert.True(payload.Success);
        Assert.Equal(1, payload.Data!.TotalCount);
        Assert.Equal(42, Assert.Single(payload.Data.Candidates).UserId);
        overview.Verify(s => s.GetCandidatesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDisconnectLog_PassesRequest_AndReturnsEntries()
    {
        var request = new GetFreeTierDisconnectLogRequest { Page = 2, PageSize = 10 };
        var overview = new Mock<IFreeTierEnforcementOverviewService>();
        overview.Setup(s => s.GetDisconnectLogAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetFreeTierDisconnectLogResponse
            {
                Entries = new PagedResponse<FreeTierDisconnectLogEntryDto>
                {
                    Page = 2,
                    PageSize = 10,
                    TotalCount = 1,
                    Items =
                    [
                        new FreeTierDisconnectLogEntryDto
                        {
                            Id = 9,
                            UserId = 42,
                            CommonName = "cn-free",
                            VpnServerId = 7,
                            KillSucceeded = true
                        }
                    ]
                }
            });

        var controller = new FreeTierEnforcementController(overview.Object);
        var result = await controller.GetDisconnectLog(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<GetFreeTierDisconnectLogResponse>>(ok.Value);
        Assert.True(payload.Success);
        Assert.Equal(9, Assert.Single(payload.Data!.Entries.Items).Id);
        overview.Verify(s => s.GetDisconnectLogAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }
}
