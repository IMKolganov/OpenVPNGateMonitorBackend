namespace DataGateMonitor.Models.Helpers;

public static class NotificationTypes
{
    public const string SystemException = "system.exception";
    public const string FileCreated     = "file.created";
    public const string CertIssued      = "cert.issued";
    public const string ServerDown      = "server.down";
    public const string ServerUp        = "server.up";

    public const string GeoLiteAutoUpdateSucceeded = "geolite.auto-update.succeeded";
    public const string GeoLiteAutoUpdateFailed     = "geolite.auto-update.failed";

    public const string UserRegistered       = "user.registered";
    public const string UserPasswordChanged  = "user.password_changed";

    public const string TrafficDailyRollupSucceeded = "traffic.daily-rollup.succeeded";
    public const string TrafficDailyRollupFailed      = "traffic.daily-rollup.failed";

    public const string OvpnCertExpiryWarning = "ovpn.cert.expiry.warning";
    public const string OvpnCertExpired       = "ovpn.cert.expired";

    public const string ServerDiscovered = "server.discovered";

    public const string PiHoleCollectorUnhealthy = "pihole.collector.unhealthy";
    public const string PiHoleCollectorRecovered = "pihole.collector.recovered";

    public const string FreeTierAccessNonCompliant = "user.free_tier.access.non_compliant";
}