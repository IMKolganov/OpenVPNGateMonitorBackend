using Microsoft.EntityFrameworkCore;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.DataBase.Services.Query.IssuedOvpnFileTable;

public class IssuedOvpnFileQueryService(IQueryService<IssuedOvpnFile, int> q) : IIssuedOvpnFileQueryService
{
    public Task<List<IssuedOvpnFile>> GetAll(CancellationToken ct)
        => q.GetAll(ct: ct);

    public Task<List<IssuedOvpnFile>> GetAllByVpnServerId(int vpnServerId, CancellationToken ct)
        => q.Where(x => x.VpnServerId == vpnServerId, ct: ct);
    
    public Task<List<IssuedOvpnFile>> GetAllByExternalId(string externalId, CancellationToken ct)
        => q.Where(x => x.ExternalId == externalId, ct: ct);

    public async Task<IReadOnlyDictionary<string, List<IssuedOvpnFile>>> GetAllByExternalIds(
        IReadOnlyCollection<string> externalIds,
        CancellationToken ct)
    {
        var ids = externalIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (ids.Count == 0)
            return new Dictionary<string, List<IssuedOvpnFile>>(StringComparer.Ordinal);

        var files = await q.Query()
            .AsNoTracking()
            .Where(x => ids.Contains(x.ExternalId))
            .ToListAsync(ct);

        return files
            .GroupBy(x => x.ExternalId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
    }

    public async Task<List<IssuedOvpnFile>> GetActiveByServerAndCommonNames(
        IReadOnlyCollection<(int VpnServerId, string CommonName)> pairs,
        CancellationToken ct)
    {
        if (pairs.Count == 0)
            return [];

        var serverIds = pairs.Select(p => p.VpnServerId).Distinct().ToList();
        var commonNames = pairs
            .Select(p => p.CommonName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (serverIds.Count == 0 || commonNames.Count == 0)
            return [];

        var pairSet = pairs
            .Where(p => !string.IsNullOrWhiteSpace(p.CommonName))
            .ToHashSet();

        var candidates = await q.Query()
            .AsNoTracking()
            .Where(x => !x.IsRevoked && serverIds.Contains(x.VpnServerId) && commonNames.Contains(x.CommonName))
            .ToListAsync(ct);

        return candidates
            .Where(x => pairSet.Contains((x.VpnServerId, x.CommonName)))
            .ToList();
    }

    public Task<List<IssuedOvpnFile>> GetAllByVpnServerIdAndIsRevoked(int vpnServerId, bool isRevoked, 
        CancellationToken ct)
        => q.Where(x => x.VpnServerId == vpnServerId && x.IsRevoked == isRevoked, ct: ct);

    public Task<List<IssuedOvpnFile>> GetAllByVpnServerIdAndExternalIdAndIsRevoked(int vpnServerId, string externalId,
        bool isRevoked, CancellationToken ct)
        => q.Where(x => x.VpnServerId == vpnServerId
                                           && x.ExternalId == externalId && x.IsRevoked == isRevoked, ct: ct);

    public Task<IssuedOvpnFile?> GetById(int id, CancellationToken ct)
        => q.FindById(id, ct: ct);

    public Task<IssuedOvpnFile?> GetByIdAndIsRevoked(int id, bool isRevoked, CancellationToken ct)
        => q.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.IsRevoked == isRevoked, ct);

    public Task<IssuedOvpnFile?> GetByIdAndVpnServerIdAndIsRevoked(int id, int vpnServerId, bool isRevoked, 
        CancellationToken ct)
        => q.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => 
                x.Id == id && x.VpnServerId == vpnServerId && x.IsRevoked == isRevoked, ct);
    
    public Task<IssuedOvpnFile?> GetByCommonNameAndVpnServerIdAndIsRevoked(string commonName, int vpnServerId, 
        bool isRevoked, CancellationToken ct)
        => q.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => 
                x.CommonName == commonName && x.VpnServerId == vpnServerId && x.IsRevoked == isRevoked, ct);
    
    public Task<string?> GetExternalIdByCommonName(string commonName, int vpnServerId, CancellationToken ct) =>
        q.Query()
            .AsNoTracking()
            .Where(x =>
                x.CommonName == commonName &&
                x.VpnServerId == vpnServerId)
            .Select(x => x.ExternalId)
            .FirstOrDefaultAsync(ct);

    public Task<IssuedOvpnFile?> GetByIdAndVpnServerIdAndCommonNameAndIsRevoked(int vpnServerId, int ovpnFileId,
        string commonName, bool isRevoked,
        CancellationToken ct)
        => q.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => 
                x.Id == ovpnFileId && x.CommonName == commonName 
                                   && x.VpnServerId == vpnServerId 
                                   && x.IsRevoked == isRevoked, ct);

    public Task<IssuedOvpnFile?> GetByVpnServerIdAndCommonName(int id, int vpnServerId, string commonName, 
        CancellationToken ct)
        => q.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => 
                x.Id == id &&
                x.CommonName == commonName 
                && x.VpnServerId == vpnServerId, ct);

    public Task<IssuedOvpnFile?> GetActiveByIdVpnServerAndCommonNameAndIsRevokedA(
        int vpnServerId, int ovpnFileId, string commonName, bool isRevoked, CancellationToken ct)
        => q.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.VpnServerId == vpnServerId
                && x.Id == ovpnFileId
                && x.CommonName == commonName
                && x.IsRevoked == isRevoked, ct);
    
    public Task<bool> ExistsActiveByVpnServerIdAndCommonName(int vpnServerId, string commonName, CancellationToken ct)
        => q.Query()
            .AsNoTracking()
            .AnyAsync(x =>
                x.VpnServerId == vpnServerId
                && x.CommonName == commonName
                && !x.IsRevoked, ct);

    public Task<List<IssuedOvpnFile>> GetAllActive(CancellationToken ct)
        => q.Where(x => !x.IsRevoked, ct: ct);

    public Task<List<IssuedOvpnFile>> GetAllActiveByVpnServerIds(IReadOnlyCollection<int> vpnServerIds, CancellationToken ct)
    {
        if (vpnServerIds.Count == 0)
            return Task.FromResult(new List<IssuedOvpnFile>());

        var ids = vpnServerIds.Distinct().ToList();
        return q.Where(x => !x.IsRevoked && ids.Contains(x.VpnServerId), ct: ct);
    }
    
    public Task<IPagedResult<IssuedOvpnFile>> GetPage(int page, int pageSize, CancellationToken ct)
        => q.Page(page, pageSize, ct: ct);
}