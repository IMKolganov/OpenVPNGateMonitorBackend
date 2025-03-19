using OpenVPNGateMonitor.Models;

namespace OpenVPNGateMonitor.SharedModels.OpenVpnFiles.Responses;

public class AddOvpnFileResponse
{
    public required FileInfo OvpnFile { get; set; }
    public required IssuedOvpnFile IssuedOvpnFile { get; set; }
}