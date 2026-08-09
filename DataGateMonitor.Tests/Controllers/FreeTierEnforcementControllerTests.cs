using DataGateMonitor.Controllers;
using DataGateMonitor.Services.Users.Interfaces;
using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Enums;
using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Responses;
using DataGateMonitor.SharedModels.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace DataGateMonitor.Tests.Controllers;

public class FreeTierEnforcementControllerTests
{
    private static FreeTierEnforcementController CreateController(
        Mock<IFreeTierEnforcementOverviewService> overview,
        Mock<IFreeTierUnsubscribedVpnUsersDailyDigestService>? digest = null,
        Mock<IFreeTierUnsubscribedUserReminderService>? reminder = null)
        => new(
            overview.Object,
            (digest ?? new Mock<IFreeTierUnsubscribedVpnUsersDailyDigestService>()).Object,
            (reminder ?? new Mock<IFreeTierUnsubscribedUserReminderService>()).Object);

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

        var controller = CreateController(overview);
        var result = await controller.GetCandidates(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<GetFreeTierEnforcementCandidatesResponse>>(ok.Value);
        Assert.True(payload.Success);
        Assert.Equal(1, payload.Data!.TotalCount);
        Assert.Equal(42, Assert.Single(payload.Data.Candidates).UserId);
    }

    [Fact]
    public async Task GetUnsubscribedVpnDigest_ReturnsStructuredDigest()
    {
        var overview = new Mock<IFreeTierEnforcementOverviewService>();
        var digest = new Mock<IFreeTierUnsubscribedVpnUsersDailyDigestService>();
        digest.Setup(s => s.BuildDigestAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FreeTierUnsubscribedVpnDigestResponse
            {
                Text = "digest-body",
                Candidates =
                [
                    new FreeTierEnforcementCandidateDto { UserId = 9, Email = "a@b.c" },
                ],
            });

        var controller = CreateController(overview, digest);
        var result = await controller.GetUnsubscribedVpnDigest(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<FreeTierUnsubscribedVpnDigestResponse>>(ok.Value);
        Assert.True(payload.Success);
        Assert.Equal("digest-body", payload.Data!.Text);
        Assert.Equal(9, Assert.Single(payload.Data.Candidates).UserId);
    }

    [Fact]
    public async Task RemindChannelSubscribe_EmailChannel_ReturnsTypedResponse()
    {
        var overview = new Mock<IFreeTierEnforcementOverviewService>();
        var reminder = new Mock<IFreeTierUnsubscribedUserReminderService>();
        reminder.Setup(r => r.ForceRemindAsync(
                "150",
                FreeTierChannelSubscribeRemindChannel.Email,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FreeTierChannelSubscribeRemindResponse.Ok(
                FreeTierChannelSubscribeRemindChannel.Email,
                "✅ sent",
                userId: 150,
                email: "t@example.com"));

        var controller = CreateController(overview, reminder: reminder);
        var result = await controller.RemindChannelSubscribe(
            "150",
            FreeTierChannelSubscribeRemindChannel.Email,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<FreeTierChannelSubscribeRemindResponse>>(ok.Value);
        Assert.True(payload.Success);
        Assert.Equal("t@example.com", payload.Data!.Email);
    }

    [Fact]
    public async Task RemindChannelSubscribe_WhenFail_ReturnsBadRequest()
    {
        var overview = new Mock<IFreeTierEnforcementOverviewService>();
        var reminder = new Mock<IFreeTierUnsubscribedUserReminderService>();
        reminder.Setup(r => r.ForceRemindAsync(
                "x",
                FreeTierChannelSubscribeRemindChannel.Telegram,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FreeTierChannelSubscribeRemindResponse.Fail(
                FreeTierChannelSubscribeRemindChannel.Telegram,
                "bad id"));

        var controller = CreateController(overview, reminder: reminder);
        var result = await controller.RemindChannelSubscribe("x", ct: CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<FreeTierChannelSubscribeRemindResponse>>(bad.Value);
        Assert.False(payload.Success);
        Assert.Equal("bad id", payload.Message);
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
                        new FreeTierDisconnectLogEntryDto { Id = 9, UserId = 1 },
                    ]
                }
            });

        var controller = CreateController(overview);
        var result = await controller.GetDisconnectLog(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<GetFreeTierDisconnectLogResponse>>(ok.Value);
        Assert.True(payload.Success);
        Assert.Equal(9, Assert.Single(payload.Data!.Entries.Items).Id);
    }
}
