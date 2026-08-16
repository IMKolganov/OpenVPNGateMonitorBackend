namespace DataGateMonitor.Services.Helpers;

/// <summary>
/// Normalize Pi-hole client subnet prefixes for StartsWith matching.
/// Dashboard often omits the trailing "." (e.g. "10.80.0" vs node "10.80.0.").
/// </summary>
public static class PiHoleClientSubnetPrefix
{
    public static string Normalize(string? raw)
    {
        var trimmed = (raw ?? "").Trim();
        if (trimmed.Length == 0 || trimmed.EndsWith('.'))
            return trimmed;

        var looksLikeIpv4Prefix = true;
        foreach (var part in trimmed.Split('.'))
        {
            if (part.Length is 0 or > 3 || !part.All(char.IsDigit))
            {
                looksLikeIpv4Prefix = false;
                break;
            }
        }

        return looksLikeIpv4Prefix ? trimmed + "." : trimmed;
    }
}
