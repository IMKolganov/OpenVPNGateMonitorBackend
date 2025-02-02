namespace OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;

public class OpenVpnSummaryStats
{
    public int ClientsCount { get; set; }
    public long BytesIn { get; set; }
    public long BytesOut { get; set; }
}