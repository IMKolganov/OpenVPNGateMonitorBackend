using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.DataBase.Services.Query.IssuedOvpnFileTable;

public interface IIssuedOvpnFileQueryService
{
    Task<List<IssuedOvpnFile>> GetAll(CancellationToken ct);
    Task<List<IssuedOvpnFile>> GetAllByVpnServerId(int vpnServerId, CancellationToken ct);
    Task<List<IssuedOvpnFile>> GetAllByExternalId(string externalId, CancellationToken ct);

    /// <summary>Batch lookup by external id (ordinal, trimmed non-empty keys).</summary>
    Task<IReadOnlyDictionary<string, List<IssuedOvpnFile>>> GetAllByExternalIds(
        IReadOnlyCollection<string> externalIds,
        CancellationToken ct);

    /// <summary>Active (non-revoked) files matching any of the server/common-name pairs.</summary>
    Task<List<IssuedOvpnFile>> GetActiveByServerAndCommonNames(
        IReadOnlyCollection<(int VpnServerId, string CommonName)> pairs,
        CancellationToken ct);

    Task<List<IssuedOvpnFile>> GetAllByVpnServerIdAndIsRevoked(int vpnServerId, bool isRevoked, CancellationToken ct);
    Task<List<IssuedOvpnFile>> GetAllByVpnServerIdAndExternalIdAndIsRevoked(int vpnServerId, string externalId,
        bool isRevoked, CancellationToken ct);
    Task<IssuedOvpnFile?> GetById(int id, CancellationToken ct);
    Task<IssuedOvpnFile?> GetByIdAndIsRevoked(int id, bool isRevoked, CancellationToken ct);

    Task<IssuedOvpnFile?> GetByIdAndVpnServerIdAndIsRevoked(int id, int vpnServerId, bool isRevoked,
        CancellationToken ct);

    Task<IssuedOvpnFile?> GetByCommonNameAndVpnServerIdAndIsRevoked(string commonName, int vpnServerId,
        bool isRevoked, CancellationToken ct);

    Task<string?> GetExternalIdByCommonName(string commonName, int vpnServerId, CancellationToken ct);
    
    Task<IssuedOvpnFile?> GetByIdAndVpnServerIdAndCommonNameAndIsRevoked(
        int vpnServerId, int ovpnFileId, string commonName, bool isRevoked, CancellationToken ct);
    
    Task<IssuedOvpnFile?> GetByVpnServerIdAndCommonName(int id, int vpnServerId, string commonName, 
        CancellationToken ct);
    
    Task<bool> ExistsActiveByVpnServerIdAndCommonName(int vpnServerId, string commonName, CancellationToken ct);

    Task<List<IssuedOvpnFile>> GetAllActive(CancellationToken ct);

    Task<List<IssuedOvpnFile>> GetAllActiveByVpnServerIds(IReadOnlyCollection<int> vpnServerIds, CancellationToken ct);

    Task<IPagedResult<IssuedOvpnFile>> GetPage(int page, int pageSize, CancellationToken ct);
}