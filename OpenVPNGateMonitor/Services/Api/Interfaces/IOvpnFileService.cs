using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Models.Helpers.Api;
using OpenVPNGateMonitor.Models.Helpers.Services;

namespace OpenVPNGateMonitor.Services.Api.Interfaces;

public interface IOvpnFileService
{
    Task<List<IssuedOvpnFile>> GetAllOvpnFiles(int vpnServerId,
        CancellationToken cancellationToken);
    Task<AddOvpnFileResponse> AddOvpnFile(string externalId, string commonName, int vpnServerId,
        CancellationToken cancellationToken, string issuedTo = "openVpnClient");
    Task<IssuedOvpnFile> RevokeOvpnFile(IssuedOvpnFile issuedOvpnFile,
        CancellationToken cancellationToken);
    Task<OvpnFileResult> GetOvpnFile(int issuedOvpnFileId, int vpnServerId,
        CancellationToken cancellationToken);
}