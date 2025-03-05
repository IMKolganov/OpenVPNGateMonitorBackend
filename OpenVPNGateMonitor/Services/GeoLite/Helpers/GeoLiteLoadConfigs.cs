using OpenVPNGateMonitor.Services.Others;

namespace OpenVPNGateMonitor.Services.GeoLite.Helpers;

public static class GeoLiteLoadConfigs
{
    public static async Task<string> GetStringParamFromSettings(string key, IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        
        var settingType = await settingsService.GetValueAsync<string>($"{key}_Type", cancellationToken) ?? 
                          throw new InvalidOperationException($"Param for {key} not found.");

        if (settingType != "string")
        {
            throw new Exception($"Setting type for {key} is not string.");
        }
        
        return await settingsService.GetValueAsync<string>(key, cancellationToken) ?? 
               throw new InvalidOperationException($"Param for {key} not found.");
    }
}