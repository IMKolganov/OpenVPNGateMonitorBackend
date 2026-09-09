using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace DataGateMonitor.SharedModels.DataGateMonitor.XrayClientLinks.Requests;

public class GetXrayClientLinksByExternalIdRequest
{
    [Required(ErrorMessage = "externalId is required.")]
    [FromRoute(Name = "externalId")]
    public string ExternalId { get; set; } = string.Empty;
}
