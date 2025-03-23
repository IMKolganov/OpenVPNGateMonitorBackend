using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Helpers.Services;
using OpenVPNGateMonitor.SharedModels.OpenVpnFiles.Responses;

namespace OpenVPNGateMonitor.Services.Api.Interfaces;

public interface IOvpnFileService
{
    Task<List<IssuedOvpnFile>> GetAllOvpnFiles(int vpnServerId,
        CancellationToken cancellationToken);
    Task<List<IssuedOvpnFile>> GetAllOvpnFilesByExternalId(int vpnServerId, string externalId,
        CancellationToken cancellationToken);
    Task<AddOvpnFileResponse> AddOvpnFile(string externalId, string commonName, int vpnServerId,
        CancellationToken cancellationToken, string issuedTo = "openVpnClient");
    Task<IssuedOvpnFile?> RevokeOvpnFile(IssuedOvpnFile issuedOvpnFile,
        CancellationToken cancellationToken);
    Task<OvpnFileResult> GetOvpnFile(int issuedOvpnFileId, int vpnServerId,
        CancellationToken cancellationToken);
}