using System.ComponentModel.DataAnnotations;

namespace DataGateMonitor.Models;

/// <summary>
/// Short-lived TV device-linking login session. Tokens are issued lazily on the first successful
/// poll after <see cref="TvLoginSessionStatus.Approved"/>, then the row becomes <see cref="TvLoginSessionStatus.Consumed"/>.
/// </summary>
public class TvLoginSession : BaseEntity<Guid>
{
    /// <summary>Normalized 8-char code without hyphen (e.g. ABCD1234).</summary>
    [Required, MaxLength(16)]
    public string UserCode { get; set; } = null!;

    public TvLoginSessionStatus Status { get; set; } = TvLoginSessionStatus.Pending;

    [MaxLength(128)]
    public string? DeviceName { get; set; }

    [MaxLength(64)]
    public string? Client { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public int? ApprovedUserId { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Optional TV client device id from create request headers.</summary>
    [MaxLength(128)]
    public string? DeviceId { get; set; }

    [MaxLength(512)]
    public string? UserAgent { get; set; }
}
