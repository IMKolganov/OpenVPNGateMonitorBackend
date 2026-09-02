namespace DataGateMonitor.SharedModels.DataGateMonitor.VpnServerClients.Responses;

public sealed class UserConnectedServerIdsResponse
{
    public List<int> VpnServerIds { get; set; } = new();
}
