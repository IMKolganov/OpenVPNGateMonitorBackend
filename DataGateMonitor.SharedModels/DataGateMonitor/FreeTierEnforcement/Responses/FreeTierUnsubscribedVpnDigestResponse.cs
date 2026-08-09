using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Dto;

namespace DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Responses;

/// <summary>
/// Preformatted admin Telegram digest plus structured candidates (for inline email buttons, etc.).
/// </summary>
public sealed class FreeTierUnsubscribedVpnDigestResponse
{
    public string Text { get; set; } = string.Empty;

    public List<FreeTierEnforcementCandidateDto> Candidates { get; set; } = [];
}
