namespace OpenVPNGateMonitor.SharedModels.OpenVpnFiles.Responses;

public class DownloadOvpnFileResponse
{
    public string FileName { get; set; } = string.Empty;
    public Stream FileStream { get; set; } = new MemoryStream();
}