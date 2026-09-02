using DataGateMonitor.Middlewares;
using DataGateMonitor.Services.Api.Auth.Login;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;

namespace DataGateMonitor.Tests.Middlewares;

public class AdminIdleActivityMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenAdminAuthenticated_TouchesIdleTracker()
    {
        var tracker = new Mock<IAdminIdleSessionTracker>();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "42"),
                new Claim(ClaimTypes.Role, "Admin"),
            ], authenticationType: "test")),
        };

        var called = false;
        RequestDelegate next = _ =>
        {
            called = true;
            return Task.CompletedTask;
        };

        var middleware = new AdminIdleActivityMiddleware(next);
        await middleware.InvokeAsync(context, tracker.Object);

        Assert.True(called);
        tracker.Verify(t => t.Touch(42), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenNonAdmin_DoesNotTouch()
    {
        var tracker = new Mock<IAdminIdleSessionTracker>();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "7"),
                new Claim(ClaimTypes.Role, "User"),
            ], authenticationType: "test")),
        };

        var middleware = new AdminIdleActivityMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context, tracker.Object);

        tracker.Verify(t => t.Touch(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WhenAnonymous_DoesNotTouch()
    {
        var tracker = new Mock<IAdminIdleSessionTracker>();
        var context = new DefaultHttpContext();

        var middleware = new AdminIdleActivityMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context, tracker.Object);

        tracker.Verify(t => t.Touch(It.IsAny<int>()), Times.Never);
    }
}
