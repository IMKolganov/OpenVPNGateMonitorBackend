using System.ComponentModel.DataAnnotations;

namespace DataGateMonitor.SharedModels.DataGateMonitor.XrayClientLinks.Requests;

public class DownloadXrayClientLinkByCnRequest
{
    [Required(ErrorMessage = "vpnServerId is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "vpnServerId must be greater than 0.")]
    public int VpnServerId { get; set; }

    [Required(ErrorMessage = "commonName is required.")]
    public string CommonName { get; set; } = string.Empty;
}
