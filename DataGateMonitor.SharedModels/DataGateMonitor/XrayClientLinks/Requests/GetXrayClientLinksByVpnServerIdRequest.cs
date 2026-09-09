using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace DataGateMonitor.SharedModels.DataGateMonitor.XrayClientLinks.Requests;

public class GetXrayClientLinksByVpnServerIdRequest
{
    [Range(1, int.MaxValue)]
    [FromRoute(Name = "vpnServerId")]
    public int VpnServerId { get; set; }
}
