using System.ComponentModel.DataAnnotations;

namespace OpenVPNGateMonitor.SharedModels.OpenVpnWebSocket.Requests;

public class GetWebSocketByServerIdRequest
{
    [Required(ErrorMessage = "openVpnServerId is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "openVpnServerId must be greater than 0.")]
    public int OpenVpnServerId { get; set; }
}