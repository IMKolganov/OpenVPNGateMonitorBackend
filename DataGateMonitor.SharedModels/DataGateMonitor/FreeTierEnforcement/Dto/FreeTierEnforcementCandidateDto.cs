namespace DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Dto;

/// <summary>
/// A user on a Free/Default plan who is not compliant (not merged and not channel-subscribed) and
/// would be disconnected by the free-tier OpenVPN session enforcement job. <see cref="IsConnected"/>
/// indicates whether they currently have an active OpenVPN session that would actually be killed on
/// the next enforcement run.
/// </summary>
public sealed class FreeTierEnforcementCandidateDto
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public long? TelegramId { get; set; }
    public string? ActivePlanName { get; set; }
    public bool IsMergedAccount { get; set; }
    public bool IsChannelSubscribed { get; set; }

    /// <summary>Identity link providers for this user (e.g. google, local, telegram), lower-case.</summary>
    public List<string> IdentityProviders { get; set; } = [];

    public bool IsConnected { get; set; }
    public int? VpnServerId { get; set; }
    public string? VpnServerName { get; set; }
    public string? CommonName { get; set; }
    public DateTimeOffset? ConnectedSince { get; set; }
}
