using System.ComponentModel.DataAnnotations;

namespace OpenVPNGateMonitor.SharedModels.OpenVpnServers.Requests;

public class GetHistoryClientsRequest
{
    [Required(ErrorMessage = "vpnServerId is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "vpnServerId must be greater than 0.")]
    public int VpnServerId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "page must be greater than 0.")]
    public int Page { get; set; } = 1;

    [Range(1, int.MaxValue, ErrorMessage = "pageSize must be greater than 0.")]
    public int PageSize { get; set; } = 10;
}