using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Models.Helpers.Api;

namespace OpenVPNGateMonitor.Services.Api.Interfaces;

public interface IOvpnFileService
{
    Task<List<IssuedOvpnFile>> GetAllOvpnFiles(int vpnServerId,
        CancellationToken cancellationToken);
    Task<AddOvpnFileResponse> AddOvpnFile(string externalId, string commonName, int vpnServerId,
        CancellationToken cancellationToken, string issuedTo = "openVpnClient");
    Task<IssuedOvpnFile> RevokeOvpnFile(IssuedOvpnFile issuedOvpnFile,
        CancellationToken cancellationToken);

}