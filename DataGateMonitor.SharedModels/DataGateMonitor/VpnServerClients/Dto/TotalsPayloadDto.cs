namespace DataGateMonitor.SharedModels.DataGateMonitor.VpnServerClients.Dto;

public sealed class TotalsPayloadDto
{
    public long SessionsCount { get; set; }

    /// <summary>Distinct non-empty <c>ExternalId</c> values (devices / client identities).</summary>
    public long UsersCount { get; set; }

    /// <summary>Distinct dashboard user accounts resolved from session <c>UserId</c> or <c>UserIdentityLink</c>.</summary>
    public long AccountsCount { get; set; }

    public long TrafficInBytes { get; set; }
    public long TrafficOutBytes { get; set; }
    public long TrafficTotalBytes => TrafficInBytes + TrafficOutBytes;
}