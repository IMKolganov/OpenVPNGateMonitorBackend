using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace DataGateMonitor.SharedModels.DataGateMonitor.XrayClientLinks.Requests;

public class GetXrayClientLinkByTokenRequest
{
    [Required(ErrorMessage = "token is required.")]
    [FromRoute(Name = "token")]
    public string Token { get; set; } = string.Empty;
}
