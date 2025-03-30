namespace OpenVPNGateMonitor.SharedModels.OpenVpnFiles.Responses;

public class AddOvpnFileApiResponse
{
    public required string FileName { get; set; }
    public required OvpnFileResponse Metadata { get; set; }
}
