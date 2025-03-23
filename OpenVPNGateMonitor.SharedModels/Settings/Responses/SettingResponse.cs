namespace OpenVPNGateMonitor.SharedModels.Settings.Responses;

public class SettingResponse
{
    public string Key { get; set; } = string.Empty;
    public object? Value { get; set; }
}