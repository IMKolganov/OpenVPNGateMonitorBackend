using DataGateMonitor.Services.Api.Auth.Login;
using DataGateMonitor.Services.Others;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DataGateMonitor.Tests.Services.Api.Auth.Login;

public class AdminIdleTimeoutProviderTests
{
    private static AdminIdleTimeoutProvider CreateSut(
        Dictionary<string, string?> configValues,
        Mock<ISettingsService>? settings = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        var services = new ServiceCollection();
        services.AddSingleton(settings?.Object ?? new Mock<ISettingsService>().Object);
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        return new AdminIdleTimeoutProvider(config, scopeFactory);
    }

    [Fact]
    public async Task GetMinutesAsync_WhenSettingsMissing_UsesJwtConfig()
    {
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(s => s.GetValueAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var sut = CreateSut(
            new Dictionary<string, string?> { ["Jwt:AdminIdleTimeoutMinutes"] = "20" },
            settings);

        Assert.Equal(20, await sut.GetMinutesAsync());
        Assert.Equal(20, sut.GetMinutes());
    }

    [Fact]
    public async Task GetMinutesAsync_WhenSettingsPresent_UsesDbValue()
    {
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(s => s.GetValueAsync<string>($"{AuthSessionSettingsKeys.AdminIdleTimeoutMinutes}_Type", It.IsAny<CancellationToken>()))
            .ReturnsAsync("int");
        settings
            .Setup(s => s.GetValueAsync<int>(AuthSessionSettingsKeys.AdminIdleTimeoutMinutes, It.IsAny<CancellationToken>()))
            .ReturnsAsync(45);

        var sut = CreateSut(
            new Dictionary<string, string?> { ["Jwt:AdminIdleTimeoutMinutes"] = "20" },
            settings);

        Assert.Equal(45, await sut.GetMinutesAsync());
    }

    [Fact]
    public async Task GetMinutesAsync_WhenDbValueTooSmall_FallsBackToDefault()
    {
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(s => s.GetValueAsync<string>($"{AuthSessionSettingsKeys.AdminIdleTimeoutMinutes}_Type", It.IsAny<CancellationToken>()))
            .ReturnsAsync("int");
        settings
            .Setup(s => s.GetValueAsync<int>(AuthSessionSettingsKeys.AdminIdleTimeoutMinutes, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var sut = CreateSut(new Dictionary<string, string?>(), settings);
        Assert.Equal(AdminIdleTimeoutProvider.DefaultMinutes, await sut.GetMinutesAsync());
    }

    [Fact]
    public async Task GetMinutesAsync_WhenDbValueTooLarge_ClampsToMax()
    {
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(s => s.GetValueAsync<string>($"{AuthSessionSettingsKeys.AdminIdleTimeoutMinutes}_Type", It.IsAny<CancellationToken>()))
            .ReturnsAsync("int");
        settings
            .Setup(s => s.GetValueAsync<int>(AuthSessionSettingsKeys.AdminIdleTimeoutMinutes, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AdminIdleTimeoutProvider.MaxMinutes + 100);

        var sut = CreateSut(new Dictionary<string, string?>(), settings);
        Assert.Equal(AdminIdleTimeoutProvider.MaxMinutes, await sut.GetMinutesAsync());
    }

    [Fact]
    public async Task GetMinutesAsync_WhenSettingsThrows_UsesConfig()
    {
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(s => s.GetValueAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var sut = CreateSut(
            new Dictionary<string, string?> { ["Jwt:AdminIdleTimeoutMinutes"] = "25" },
            settings);

        Assert.Equal(25, await sut.GetMinutesAsync());
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(15, 15)]
    [InlineData(0, 15)]
    [InlineData(-3, 15)]
    [InlineData(10_000, 1440)]
    public void Clamp_EnforcesBounds(int input, int expected)
    {
        Assert.Equal(expected, AdminIdleTimeoutProvider.Clamp(input));
    }
}
