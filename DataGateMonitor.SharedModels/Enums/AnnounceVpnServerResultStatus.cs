namespace DataGateMonitor.SharedModels.Enums;

/// <summary>Outcome of a node self-announce to the dashboard.</summary>
public enum AnnounceVpnServerResultStatus
{
    Pending = 0,
    AlreadyRegistered = 1,
    Rejected = 2,
}
