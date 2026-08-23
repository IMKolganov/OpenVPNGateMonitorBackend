using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using DataGateMonitor.Middlewares;
using DataGateMonitor.Services.Others.Notifications;
using Xunit;

namespace DataGateMonitor.Tests.Middlewares;

public class GlobalExceptionMiddlewareTests
{
    private static HttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static (GlobalExceptionMiddleware middleware, Mock<IAppNotificationFacade> facade) CreateMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware>? logger = null)
    {
        var facadeMock = new Mock<IAppNotificationFacade>();
        facadeMock
            .Setup(f => f.SystemException(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var scopeProvider = new Mock<IServiceProvider>();
        scopeProvider.Setup(p => p.GetService(typeof(IAppNotificationFacade))).Returns(facadeMock.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(scopeProvider.Object);
        scope.Setup(s => s.Dispose()).Verifiable();

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var rootProvider = new Mock<IServiceProvider>();
        rootProvider.Setup(p => p.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactory.Object);

        logger ??= Mock.Of<ILogger<GlobalExceptionMiddleware>>();
        var middleware = new GlobalExceptionMiddleware(next, rootProvider.Object, logger);
        return (middleware, facadeMock);
    }

    private static async Task<string> ReadResponseBodyAsync(HttpResponse response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body);
        return await reader.ReadToEndAsync();
    }

    [Fact]
    public async Task InvokeAsync_WhenNextThrowsUnauthorizedAccessException_Returns401_AndMessageInBody()
    {
        var context = CreateContext();
        RequestDelegate next = _ => throw new UnauthorizedAccessException("Invalid login or password.");
        var (middleware, facade) = CreateMiddleware(next);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);
        var body = await ReadResponseBodyAsync(context.Response);
        var json = JObject.Parse(body);
        Assert.Equal(401, json["statusCode"]?.Value<int>());
        Assert.Equal("Invalid login or password.", json["message"]?.Value<string>());
        Assert.Equal("Invalid login or password.", json["detail"]?.Value<string>());
        facade.Verify(
            f => f.SystemException(It.IsAny<UnauthorizedAccessException>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WhenAdminIdleSessionExpired_Returns401_AndDoesNotNotifyAdmins()
    {
        var context = CreateContext();
        RequestDelegate next = _ => throw new UnauthorizedAccessException(
            "Administrator session expired due to inactivity.");
        var (middleware, facade) = CreateMiddleware(next);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        var body = await ReadResponseBodyAsync(context.Response);
        var json = JObject.Parse(body);
        Assert.Equal("Administrator session expired due to inactivity.", json["message"]?.Value<string>());
        facade.Verify(f => f.SystemException(It.IsAny<Exception>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WhenNextThrowsArgumentException_Returns400_AndMessageInBody()
    {
        var context = CreateContext();
        RequestDelegate next = _ => throw new ArgumentException("Invalid argument.");
        var (middleware, facade) = CreateMiddleware(next);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        var body = await ReadResponseBodyAsync(context.Response);
        var json = JObject.Parse(body);
        Assert.Equal(400, json["statusCode"]?.Value<int>());
        Assert.Equal("Invalid argument.", json["message"]?.Value<string>());
        facade.Verify(f => f.SystemException(It.IsAny<ArgumentException>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenNextThrowsDuplicateVpnServerName_Returns409_AndMessageInBody()
    {
        var context = CreateContext();
        RequestDelegate next = _ => throw new InvalidOperationException("A VPN server with the same name already exists.");
        var (middleware, facade) = CreateMiddleware(next);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        var body = await ReadResponseBodyAsync(context.Response);
        var json = JObject.Parse(body);
        Assert.Equal(409, json["statusCode"]?.Value<int>());
        Assert.Equal("A VPN server with the same name already exists.", json["message"]?.Value<string>());
        Assert.Equal("A VPN server with the same name already exists.", json["detail"]?.Value<string>());
    }

    [Fact]
    public async Task InvokeAsync_WhenDbUniqueVpnServerName_Returns409_WithoutSqlLeak()
    {
        var context = CreateContext();
        var pg = new Npgsql.PostgresException(
            "duplicate key value violates unique constraint \"IX_VpnServers_ServerName\"",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: "23505");
        RequestDelegate next = _ => throw new Microsoft.EntityFrameworkCore.DbUpdateException("Save failed", pg);
        var (middleware, _) = CreateMiddleware(next);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        var body = await ReadResponseBodyAsync(context.Response);
        var json = JObject.Parse(body);
        Assert.Equal("A VPN server with the same name already exists.", json["message"]?.Value<string>());
        Assert.Equal("A VPN server with the same name already exists.", json["detail"]?.Value<string>());
        Assert.DoesNotContain("SQLSTATE", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvokeAsync_WhenNextThrowsGenericException_Returns500_AndGenericMessage()
    {
        var context = CreateContext();
        RequestDelegate next = _ => throw new InvalidOperationException("Internal failure.");
        var (middleware, facade) = CreateMiddleware(next);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        var body = await ReadResponseBodyAsync(context.Response);
        var json = JObject.Parse(body);
        Assert.Equal(500, json["statusCode"]?.Value<int>());
        Assert.Equal("An unexpected error occurred. Please try again later.", json["message"]?.Value<string>());
        Assert.Equal("Internal failure.", json["detail"]?.Value<string>());
        facade.Verify(f => f.SystemException(It.IsAny<InvalidOperationException>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenNextDoesNotThrow_CallsNext_AndNoResponseWritten()
    {
        var context = CreateContext();
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };
        var (middleware, facade) = CreateMiddleware(next);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode); // default
        facade.Verify(f => f.SystemException(It.IsAny<Exception>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WhenNotificationFails_StillReturns500_AndLogs()
    {
        var context = CreateContext();
        RequestDelegate next = _ => throw new Exception("Boom.");
        var facadeMock = new Mock<IAppNotificationFacade>();
        facadeMock.Setup(f => f.SystemException(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Notification failed."));

        var scopeProvider = new Mock<IServiceProvider>();
        scopeProvider.Setup(p => p.GetService(typeof(IAppNotificationFacade))).Returns(facadeMock.Object);
        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(scopeProvider.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);
        var rootProvider = new Mock<IServiceProvider>();
        rootProvider.Setup(p => p.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactory.Object);

        var logger = Mock.Of<ILogger<GlobalExceptionMiddleware>>();
        var middleware = new GlobalExceptionMiddleware(next, rootProvider.Object, logger);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        var body = await ReadResponseBodyAsync(context.Response);
        var json = JObject.Parse(body);
        Assert.Equal(500, json["statusCode"]?.Value<int>());
        Assert.Equal("Boom.", json["detail"]?.Value<string>());
    }

    [Fact]
    public async Task InvokeAsync_WhenClientNavigationCancel_Returns499()
    {
        var context = CreateContext();
        RequestDelegate next = _ => throw new OperationCanceledException("The operation was canceled.");
        var (middleware, facade) = CreateMiddleware(next);

        await middleware.InvokeAsync(context);

        Assert.Equal(499, context.Response.StatusCode);
        var body = await ReadResponseBodyAsync(context.Response);
        var json = JObject.Parse(body);
        Assert.Equal(499, json["statusCode"]?.Value<int>());
        facade.Verify(f => f.SystemException(It.IsAny<Exception>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
