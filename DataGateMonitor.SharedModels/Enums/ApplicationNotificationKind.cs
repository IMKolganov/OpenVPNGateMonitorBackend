namespace DataGateMonitor.SharedModels.Enums;

/// <summary>
/// One persisted toggle per admin-notification family. Values 0–5 match legacy (stack × category) as (stack * 3 + category).
/// </summary>
public enum ApplicationNotificationKind
{
    // OpenVPN profile API (stack 0)
    OpenVpnProfileRead = 0,
    OpenVpnProfileMutate = 1,
    OpenVpnProfileDownload = 2,

    // Xray client link API (stack 1)
    XrayProfileRead = 3,
    XrayProfileMutate = 4,
    XrayProfileDownload = 5,

    // Certificate microservice API (cert-api)
    CertApiReadAll = 6,
    CertApiCertificateCreated = 7,
    CertApiCertificateRevoked = 8,

    // OpenVPN server CRUD / sync (server-openvpn-api)
    OpenVpnServerBecameAvailable = 9,
    OpenVpnServerAdded = 10,
    OpenVpnServerUpdated = 11,
    OpenVpnServerDeleted = 12,
    OpenVpnServerBecameUnavailable = 13,
    OpenVpnServerSyncError = 14,
    OpenVpnServerNoResponse = 15,

    // OpenVPN microservice client
    OpenVpnMicroserviceSendCommandFailed = 16,
    OpenVpnMicroserviceReconnectFailed = 17,
    OpenVpnMicroserviceEventHubConnectionFailed = 18,
    OpenVpnMicroserviceProxyClientLookupFailed = 19,

    // GeoLite automatic update
    GeoLiteAutoUpdateSucceeded = 20,
    GeoLiteAutoUpdateFailed = 21,

    // In-app catalog / monitor / backend events
    AppUnhandledException = 22,
    AppFileCreated = 23,
    AppCertificateIssued = 24,
    AppServerMonitorDown = 25,
    AppServerMonitorUp = 26,

    /// <summary>New dashboard user registered (self-service).</summary>
    AppUserRegistered = 27,

    /// <summary>Password hash changed (forgot/reset flow or future profile password change). Not used for successful login.</summary>
    AppUserPasswordChanged = 28,

    /// <summary>Daily traffic rollup completed successfully.</summary>
    TrafficDailyRollupSucceeded = 29,

    /// <summary>Daily traffic rollup failed.</summary>
    TrafficDailyRollupFailed = 30,

    /// <summary>Issued OpenVPN client certificate expires within the configured warning window.</summary>
    OvpnCertExpiryWarning = 31,

    /// <summary>Issued OpenVPN client certificate has expired on the node PKI.</summary>
    OvpnCertExpired = 32,

    /// <summary>VPN node announced itself; admin should approve or deny adding it to the server list.</summary>
    OpenVpnServerDiscovered = 33,
}
