using System.ComponentModel.DataAnnotations;

namespace DataGateMonitor.SharedModels.DataGateMonitor.XrayClientLinks.Requests;

public class DownloadXrayClientLinkRequest
{
    [Required(ErrorMessage = "issuedXrayClientLinkId is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "issuedXrayClientLinkId must be greater than 0.")]
    public int IssuedXrayClientLinkId { get; set; }

    [Required(ErrorMessage = "vpnServerId is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "vpnServerId must be greater than 0.")]
    public int VpnServerId { get; set; }
}
