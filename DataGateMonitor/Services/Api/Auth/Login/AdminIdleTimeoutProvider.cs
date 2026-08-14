using DataGateMonitor.Services.Others;

namespace DataGateMonitor.Services.Api.Auth.Login;

public interface IAdminIdleTimeoutProvider
{
    /// <summary>Effective idle timeout minutes (Settings DB, then Jwt:AdminIdleTimeoutMinutes, then 15).</summary>
    Task<int> GetMinutesAsync(CancellationToken cancellationToken = default);

    /// <summary>Synchronous resolve for idle tracker (Settings via scoped service, config fallback).</summary>
    int GetMinutes();
}

public sealed class AdminIdleTimeoutProvider(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory) : IAdminIdleTimeoutProvider
{
    public const int DefaultMinutes = 15;
    public const int MinMinutes = 1;
    public const int MaxMinutes = 24 * 60;

    public async Task<int> GetMinutesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var fromDb = await TryReadFromSettingsAsync(settings, cancellationToken);
            if (fromDb is int dbMinutes)
                return Clamp(dbMinutes);
        }
        catch
        {
            // fall through to config
        }

        return Clamp(ReadFromConfig(configuration));
    }

    public int GetMinutes()
    {
        try
        {
            return GetMinutesAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch
        {
            return Clamp(ReadFromConfig(configuration));
        }
    }

    internal static int Clamp(int minutes)
    {
        if (minutes < MinMinutes) return DefaultMinutes;
        if (minutes > MaxMinutes) return MaxMinutes;
        return minutes;
    }

    private static int ReadFromConfig(IConfiguration configuration)
    {
        var minutes = configuration.GetValue<int?>("Jwt:AdminIdleTimeoutMinutes") ?? DefaultMinutes;
        return minutes <= 0 ? DefaultMinutes : minutes;
    }

    private static async Task<int?> TryReadFromSettingsAsync(ISettingsService settings, CancellationToken ct)
    {
        var typeKey = $"{AuthSessionSettingsKeys.AdminIdleTimeoutMinutes}_Type";
        var type = await settings.GetValueAsync<string>(typeKey, ct);
        if (!string.Equals(type, "int", StringComparison.OrdinalIgnoreCase))
            return null;

        var value = await settings.GetValueAsync<int>(AuthSessionSettingsKeys.AdminIdleTimeoutMinutes, ct);
        return value;
    }
}
