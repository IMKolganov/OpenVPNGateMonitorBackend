using System.ComponentModel.DataAnnotations;

namespace OpenVPNGateMonitor.SharedModels.OpenVpnWebSocket.Requests;

public class GetWebSocketByIpRequest
{
    [Required(ErrorMessage = "IP address is required.")]
    [RegularExpression(@"^(?:\d{1,3}\.){3}\d{1,3}$", ErrorMessage = "Invalid IP address format.")]
    public string Ip { get; set; } = string.Empty;

    [Required(ErrorMessage = "Port is required.")]
    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535.")]
    public int Port { get; set; }
}