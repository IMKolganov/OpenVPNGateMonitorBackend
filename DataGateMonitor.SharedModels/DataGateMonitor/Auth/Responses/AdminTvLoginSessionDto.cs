namespace DataGateMonitor.SharedModels.DataGateMonitor.Auth.Responses;

/// <summary>Admin view of a TV device-linking session row.</summary>
public sealed class AdminTvLoginSessionDto
{
    public Guid SessionId { get; set; }

    /// <summary>6-digit user code shown on the TV.</summary>
    public string UserCode { get; set; } = null!;

    /// <summary>
    /// Lowercase status: pending, viewed, approved, denied, expired, consumed.
    /// </summary>
    public string Status { get; set; } = null!;

    public string? DeviceName { get; set; }
    public string? Client { get; set; }
    public string? DeviceId { get; set; }
    public string? UserAgent { get; set; }

    public int? ApprovedUserId { get; set; }
    public string? ApprovedUserEmail { get; set; }
    public string? ApprovedUserDisplayName { get; set; }

    public DateTimeOffset CreateDate { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class GetAdminTvLoginSessionsResponse
{
    public IReadOnlyList<AdminTvLoginSessionDto> Sessions { get; set; } = [];
    public int TotalCount { get; set; }
}
