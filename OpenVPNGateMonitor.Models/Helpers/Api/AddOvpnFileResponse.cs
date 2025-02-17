namespace OpenVPNGateMonitor.Models.Helpers.Api;

public class AddOvpnFileResponse
{
    public required FileInfo OvpnFile { get; set; }
    public required IssuedOvpnFile IssuedOvpnFile { get; set; }
}