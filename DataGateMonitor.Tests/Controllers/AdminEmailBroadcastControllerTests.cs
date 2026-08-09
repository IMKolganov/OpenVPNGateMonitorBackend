using System.Security.Claims;
using DataGateMonitor.Controllers;
using DataGateMonitor.Services.AdminEmail;
using DataGateMonitor.SharedModels.DataGateMonitor.EmailBroadcast.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.EmailBroadcast.Responses;
using DataGateMonitor.SharedModels.DataGateMonitor.EmailBroadcast.Responses.Dto;
using DataGateMonitor.SharedModels.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace DataGateMonitor.Tests.Controllers;

public class AdminEmailBroadcastControllerTests
{
    [Fact]
    public async Task GetHistory_ReturnsPagedPayload()
    {
        var request = new GetSentEmailHistoryRequest { Page = 1, PageSize = 20 };
        var broadcast = new Mock<IAdminEmailBroadcastService>();
        broadcast.Setup(s => s.GetHistoryAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSentEmailHistoryResponse
            {
                Page = 1,
                PageSize = 20,
                TotalCount = 1,
                Items = [new SentEmailLogDto { Id = 3, Subject = "Hello" }]
            });

        var controller = CreateController(broadcast.Object, Mock.Of<IAdminEmailTemplateService>());
        var result = await controller.GetHistory(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<GetSentEmailHistoryResponse>>(ok.Value);
        Assert.True(payload.Success);
        Assert.Equal(3, Assert.Single(payload.Data!.Items).Id);
    }

    [Fact]
    public async Task Send_PassesCurrentUserId_AndReturnsOk()
    {
        var request = new SendAdminEmailRequest { Subject = "Subj", HtmlBody = "<p>Hi</p>" };
        var broadcast = new Mock<IAdminEmailBroadcastService>();
        broadcast.Setup(s => s.SendAsync(request, 77, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendAdminEmailResponse { Attempted = 2, Succeeded = 2, Failed = 0 });

        var controller = CreateController(broadcast.Object, Mock.Of<IAdminEmailTemplateService>(), userId: 77);
        var result = await controller.Send(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<SendAdminEmailResponse>>(ok.Value);
        Assert.True(payload.Success);
        Assert.Equal(2, payload.Data!.Succeeded);
        broadcast.Verify(s => s.SendAsync(request, 77, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Send_WhenArgumentException_ReturnsBadRequest()
    {
        var request = new SendAdminEmailRequest { Subject = "", HtmlBody = "" };
        var broadcast = new Mock<IAdminEmailBroadcastService>();
        broadcast.Setup(s => s.SendAsync(request, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Subject is required."));

        var controller = CreateController(broadcast.Object, Mock.Of<IAdminEmailTemplateService>());
        var result = await controller.Send(request, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<SendAdminEmailResponse>>(bad.Value);
        Assert.False(payload.Success);
        Assert.Equal("Subject is required.", payload.Message);
    }

    [Fact]
    public async Task ListTemplates_ReturnsSummaries()
    {
        var templates = new Mock<IAdminEmailTemplateService>();
        templates.Setup(s => s.ListSummariesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetEmailTemplatesResponse
            {
                Items = [new EmailBroadcastTemplateSummaryDto { Id = 1, Name = "Welcome", Subject = "Hi" }]
            });

        var controller = CreateController(Mock.Of<IAdminEmailBroadcastService>(), templates.Object);
        var result = await controller.ListTemplates(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<GetEmailTemplatesResponse>>(ok.Value);
        Assert.Equal("Welcome", Assert.Single(payload.Data!.Items).Name);
    }

    [Fact]
    public async Task GetTemplate_WhenMissing_ReturnsNotFound()
    {
        var templates = new Mock<IAdminEmailTemplateService>();
        templates.Setup(s => s.GetByIdAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailBroadcastTemplateDto?)null);

        var controller = CreateController(Mock.Of<IAdminEmailBroadcastService>(), templates.Object);
        var result = await controller.GetTemplate(9, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetTemplate_WhenFound_ReturnsOk()
    {
        var templates = new Mock<IAdminEmailTemplateService>();
        templates.Setup(s => s.GetByIdAsync(4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailBroadcastTemplateDto { Id = 4, Name = "Promo", Subject = "Deal" });

        var controller = CreateController(Mock.Of<IAdminEmailBroadcastService>(), templates.Object);
        var result = await controller.GetTemplate(4, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<EmailBroadcastTemplateDto>>(ok.Value);
        Assert.Equal(4, payload.Data!.Id);
    }

    [Fact]
    public async Task CreateTemplate_PassesCreatedByUserId()
    {
        var request = new CreateEmailTemplateRequest
        {
            Name = "N",
            Subject = "S",
            HtmlBody = "<b>x</b>"
        };
        var templates = new Mock<IAdminEmailTemplateService>();
        templates.Setup(s => s.CreateAsync(request, 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailBroadcastTemplateDto { Id = 8, Name = "N", CreatedByUserId = 12 });

        var controller = CreateController(Mock.Of<IAdminEmailBroadcastService>(), templates.Object, userId: 12);
        var result = await controller.CreateTemplate(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<EmailBroadcastTemplateDto>>(ok.Value);
        Assert.Equal(8, payload.Data!.Id);
        templates.Verify(s => s.CreateAsync(request, 12, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateTemplate_WhenNotFound_ReturnsNotFound()
    {
        var request = new UpdateEmailTemplateRequest { Name = "N", Subject = "S", HtmlBody = "H" };
        var templates = new Mock<IAdminEmailTemplateService>();
        templates.Setup(s => s.UpdateAsync(3, request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Template not found."));

        var controller = CreateController(Mock.Of<IAdminEmailBroadcastService>(), templates.Object);
        var result = await controller.UpdateTemplate(3, request, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<EmailBroadcastTemplateDto>>(notFound.Value);
        Assert.False(payload.Success);
    }

    [Fact]
    public async Task DeleteTemplate_DelegatesAndReturnsOk()
    {
        var templates = new Mock<IAdminEmailTemplateService>();
        templates.Setup(s => s.DeleteAsync(6, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var controller = CreateController(Mock.Of<IAdminEmailBroadcastService>(), templates.Object);
        var result = await controller.DeleteTemplate(6, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<bool>>(ok.Value);
        Assert.True(payload.Data);
        templates.Verify(s => s.DeleteAsync(6, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteTemplate_WhenMissing_ReturnsNotFound()
    {
        var templates = new Mock<IAdminEmailTemplateService>();
        templates.Setup(s => s.DeleteAsync(6, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("gone"));

        var controller = CreateController(Mock.Of<IAdminEmailBroadcastService>(), templates.Object);
        var result = await controller.DeleteTemplate(6, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    private static AdminEmailBroadcastController CreateController(
        IAdminEmailBroadcastService broadcast,
        IAdminEmailTemplateService templates,
        int? userId = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, "Admin") };
        if (userId is int id)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, id.ToString()));

        var controller = new AdminEmailBroadcastController(broadcast, templates);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"))
            }
        };
        return controller;
    }
}
