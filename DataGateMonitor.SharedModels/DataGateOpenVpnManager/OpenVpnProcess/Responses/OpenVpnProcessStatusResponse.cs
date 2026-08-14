namespace DataGateMonitor.SharedModels.DataGateOpenVpnManager.OpenVpnProcess.Responses;

/// <summary>Result of OpenVPN daemon start / restart / kill / status on the node manager.</summary>
public class OpenVpnProcessStatusResponse
{
    public string Action { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public int? Pid { get; set; }
    public string? ConfigPath { get; set; }
    public string? PidFilePath { get; set; }
    public string Message { get; set; } = string.Empty;
}
