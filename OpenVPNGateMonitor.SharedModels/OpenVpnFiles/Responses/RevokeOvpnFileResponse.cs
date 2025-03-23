namespace OpenVPNGateMonitor.SharedModels.OpenVpnFiles.Responses;

public class RevokeOvpnFileResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
