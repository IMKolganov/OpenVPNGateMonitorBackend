namespace OpenVPNGateMonitor.Models.Helpers.Services;

public class OvpnFileResult
{
    public FileStream? FileStream { get; set; }
    public string FileName { get; set; } = string.Empty;
}