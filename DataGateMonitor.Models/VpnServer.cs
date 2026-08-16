using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Models;

public class VpnServer : BaseEntity<int>
{
    /// <summary>VPN/proxy stack (OpenVPN, Xray, …).</summary>
    public VpnServerType ServerType { get; set; } = VpnServerType.OpenVpn;

    public string ServerName { get; set; } = string.Empty;
    public bool IsOnline { get; set; } = false;
    public bool IsDefault { get; set; } = false;
    public bool IsDisable { get; set; } = false;
    public string ApiUrl { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsEnableWss { get; set; } = false;
    public bool IsPiHoleEnabled { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
    /// <summary>DCO (Data Channel Offload) enabled; from OpenVPN "status 3" GLOBAL_STATS dco_enabled. Not required in DB.</summary>
    public bool? DcoIsEnabled { get; set; }

    /// <summary>Last time the monitor polled the Xray node <c>GET /api/xray/clients</c> (any outcome).</summary>
    public DateTimeOffset? XrayClientsPolledAt { get; set; }

    /// <summary>Last error from Xray node client poll (HTTP failure or node-reported <c>PollError</c>).</summary>
    public string? XrayClientsPollError { get; set; }

    /// <summary>Optional UI folder for the servers list. Null = ungrouped.</summary>
    public int? VpnServerGroupId { get; set; }

    /// <summary>Order within the group (or among ungrouped servers when <see cref="VpnServerGroupId"/> is null).</summary>
    public int SortOrder { get; set; }
}