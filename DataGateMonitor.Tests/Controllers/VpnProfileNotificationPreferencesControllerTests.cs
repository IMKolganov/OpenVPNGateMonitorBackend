using DataGateMonitor.Controllers;
using DataGateMonitor.Services.Others.Notifications;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnProfileNotificationPreferences;
using DataGateMonitor.SharedModels.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace DataGateMonitor.Tests.Controllers;

public class VpnProfileNotificationPreferencesControllerTests
{
    [Fact]
    public async Task Get_ReturnsServicePayload()
    {
        var prefs = new Mock<IVpnProfileNotificationPreferenceService>();
        prefs.Setup(p => p.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetVpnProfileNotificationPreferencesResponse { Preferences = [] });

        var controller = new VpnProfileNotificationPreferencesController(prefs.Object);
        var result = await controller.Get(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<GetVpnProfileNotificationPreferencesResponse>>(ok.Value);
        Assert.True(payload.Success);
        Assert.NotNull(payload.Data!.Preferences);
        prefs.Verify(p => p.GetAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Put_UpdatesThenReturnsCurrentPreferences()
    {
        var request = new PutVpnProfileNotificationPreferencesRequest();
        var prefs = new Mock<IVpnProfileNotificationPreferenceService>();
        prefs.Setup(p => p.UpdateAsync(request, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        prefs.Setup(p => p.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetVpnProfileNotificationPreferencesResponse { Preferences = [] });

        var controller = new VpnProfileNotificationPreferencesController(prefs.Object);
        var result = await controller.Put(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(Assert.IsType<ApiResponse<GetVpnProfileNotificationPreferencesResponse>>(ok.Value).Success);
        prefs.Verify(p => p.UpdateAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        prefs.Verify(p => p.GetAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetAllCategories_EnablesAllThenReturnsPreferences()
    {
        var prefs = new Mock<IVpnProfileNotificationPreferenceService>();
        prefs.Setup(p => p.SetAllPreferencesEnabledAsync(true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        prefs.Setup(p => p.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetVpnProfileNotificationPreferencesResponse { Preferences = [] });

        var controller = new VpnProfileNotificationPreferencesController(prefs.Object);
        var result = await controller.SetAllCategories(
            new SetAllVpnProfileNotificationCategoriesRequest { Enabled = true },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(Assert.IsType<ApiResponse<GetVpnProfileNotificationPreferencesResponse>>(ok.Value).Success);
        prefs.Verify(p => p.SetAllPreferencesEnabledAsync(true, It.IsAny<CancellationToken>()), Times.Once);
    }
}
