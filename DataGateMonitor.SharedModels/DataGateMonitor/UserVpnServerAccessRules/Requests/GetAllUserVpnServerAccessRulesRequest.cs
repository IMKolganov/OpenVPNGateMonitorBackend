using System.ComponentModel.DataAnnotations;

namespace DataGateMonitor.SharedModels.DataGateMonitor.UserVpnServerAccessRules.Requests;

public class GetAllUserVpnServerAccessRulesRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0.")]
    public int Page { get; set; } = 1;

    [Range(1, 500, ErrorMessage = "PageSize must be between 1 and 500.")]
    public int PageSize { get; set; } = 20;

    [Range(0, int.MaxValue)]
    public int? UserId { get; set; }

    [Range(0, int.MaxValue)]
    public int? VpnServerId { get; set; }
}
