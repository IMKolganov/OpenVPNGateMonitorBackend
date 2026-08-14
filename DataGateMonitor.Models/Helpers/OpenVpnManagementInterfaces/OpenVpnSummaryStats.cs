namespace DataGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;

public class OpenVpnSummaryStats
{
    public int ClientsCount { get; set; }
    public long BytesIn { get; set; }
    public long BytesOut { get; set; }

    /// <summary>From GLOBAL_STATS dco_enabled on status 3. Null if not reported.</summary>
    public bool? DcoEnabled { get; set; }
}
