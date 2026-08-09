using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using DataGateMonitor.Configurations;
using DataGateMonitor.Services.Performance;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DataGateMonitor.Tests.Configurations;

public class MiddlewareConfigurationTests
{
    [Fact]
    public void ConfigureMiddleware_DoesNotThrow()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddLogging();
        builder.Services.AddSingleton(Mock.Of<IPerformanceSampleStore>());
        builder.Services.AddSingleton(Options.Create(new PerformanceMonitoringOptions()));
        var app = builder.Build();
        app.UseRouting();

        var exception = Record.Exception(() => app.ConfigureMiddleware());

        Assert.Null(exception);
    }
}
