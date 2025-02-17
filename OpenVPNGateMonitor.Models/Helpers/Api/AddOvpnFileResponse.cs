namespace OpenVPNGateMonitor.Models.Helpers.Api;

public class AddOvpnFileResponse
{
    public FileInfo OvpnFile { get; set; }
    public IssuedOvpnFile IssuedOvpnFile { get; set; }
}