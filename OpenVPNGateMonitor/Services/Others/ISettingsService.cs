namespace OpenVPNGateMonitor.Services.Others;

public interface ISettingsService
{
    Task<T?> GetValueAsync<T>(string key, CancellationToken cancellationToken);
    Task SetValueAsync<T>(string key, T value, CancellationToken cancellationToken);
}