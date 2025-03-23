using System.ComponentModel.DataAnnotations;

namespace OpenVPNGateMonitor.SharedModels.OpenVpnFiles.Requests;

public class GetAllOvpnFilesRequest
{
    [Required]
    public int VpnServerId { get; set; }
}