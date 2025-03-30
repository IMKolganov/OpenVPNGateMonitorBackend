using Microsoft.EntityFrameworkCore;
using OpenVPNGateMonitor.DataBase.UnitOfWork;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Helpers.Services;
using OpenVPNGateMonitor.Services.Api.Interfaces;
using OpenVPNGateMonitor.Services.Helpers;

namespace OpenVPNGateMonitor.Services.Api;

public class VpnDataService : IVpnDataService
{
    private readonly ILogger<IVpnDataService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ExternalIpAddressService _externalIpAddressService;
    
    public VpnDataService(ILogger<IVpnDataService> logger, IUnitOfWork unitOfWork, 
        ExternalIpAddressService externalIpAddressService)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _externalIpAddressService = externalIpAddressService;
    }

    public async Task<OpenVpnServerClientList> GetAllConnectedOpenVpnServerClients(
        int vpnServerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var openVpnServerClients = await _unitOfWork.GetQuery<OpenVpnServerClient>()
            .AsQueryable()
            .Where(x => x.IsConnected && x.VpnServerId == vpnServerId)
            .OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        
        return new OpenVpnServerClientList(){ OpenVpnServerClients = openVpnServerClients, TotalCount = openVpnServerClients.Count };
    }

    public async Task<OpenVpnServerClientList> GetAllHistoryOpenVpnServerClients(
        int vpnServerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var openVpnServerClients = await _unitOfWork.GetQuery<OpenVpnServerClient>()
            .AsQueryable()
            .Where(x => x.VpnServerId == vpnServerId)
            .OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var totalCount = await _unitOfWork.GetQuery<OpenVpnServerClient>().AsQueryable().CountAsync(cancellationToken);
        
        return new OpenVpnServerClientList(){ OpenVpnServerClients = openVpnServerClients, TotalCount = totalCount };
    }

    public async Task<List<OpenVpnServerWithStatus>> GetAllOpenVpnServersWithStatus(CancellationToken cancellationToken)
    {
        var openVpnServers = await _unitOfWork.GetQuery<OpenVpnServer>()
            .AsQueryable()
            .OrderByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
        
        var serverTraffic = await _unitOfWork.GetQuery<OpenVpnServerStatusLog>()
            .AsQueryable()
            .GroupBy(x => x.VpnServerId)
            .Select(g => new
            {
                ServerId = g.Key,
                TotalBytesIn = g.Sum(x => x.BytesIn),
                TotalBytesOut = g.Sum(x => x.BytesOut)
            })
            .ToDictionaryAsync(x => x.ServerId, cancellationToken);

        var openVpnServerInfoResponses = new List<OpenVpnServerWithStatus>();

        foreach (var openVpnServer in openVpnServers)
        {
            var countConnectedClients = await _unitOfWork.GetQuery<OpenVpnServerClient>()
                .AsQueryable()
                .Where(x => x.IsConnected && x.VpnServerId == openVpnServer.Id)
                .CountAsync(cancellationToken);
            var countSessions = await _unitOfWork.GetQuery<OpenVpnServerClient>()
                .AsQueryable()
                .Where(x => x.VpnServerId == openVpnServer.Id)
                .CountAsync(cancellationToken);
            
            var openVpnServerStatusLog = await _unitOfWork.GetQuery<OpenVpnServerStatusLog>()
                .AsQueryable()
                .Where(x => x.VpnServerId == openVpnServer.Id)
                .OrderBy(x => x.Id)
                .LastOrDefaultAsync(cancellationToken);
            
            var totalBytesIn = serverTraffic.ContainsKey(openVpnServer.Id) ? serverTraffic[openVpnServer.Id].TotalBytesIn : 0;
            var totalBytesOut = serverTraffic.ContainsKey(openVpnServer.Id) ? serverTraffic[openVpnServer.Id].TotalBytesOut : 0;
            
            openVpnServerInfoResponses.Add(new OpenVpnServerWithStatus()
            {
                OpenVpnServer = openVpnServer,
                OpenVpnServerStatusLog = openVpnServerStatusLog,
                CountConnectedClients = countConnectedClients,
                CountSessions = countSessions,
                TotalBytesIn = totalBytesIn,
                TotalBytesOut = totalBytesOut
            });
        }



        return openVpnServerInfoResponses;
    }
    
    public async Task<OpenVpnServerWithStatus> GetOpenVpnServerWithStatus(int vpnServerId, CancellationToken cancellationToken)
    {
        var openVpnServer = await _unitOfWork.GetQuery<OpenVpnServer>()
            .AsQueryable()
            .Where(x=> x.Id == vpnServerId)
            .OrderByDescending(x=>x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        
        if (openVpnServer == null) throw new NullReferenceException("OpenVPN Server not found");
        
        var openVpnServerStatusLog = await _unitOfWork.GetQuery<OpenVpnServerStatusLog>()
            .AsQueryable()
            .Where(x => x.VpnServerId == openVpnServer.Id)
            .OrderBy(x=>x.Id)
            .LastOrDefaultAsync(cancellationToken);
        
        var openVpnServerInfoResponse = new OpenVpnServerWithStatus()
        {
            OpenVpnServer = openVpnServer,
            OpenVpnServerStatusLog = openVpnServerStatusLog
        };

        return openVpnServerInfoResponse;
    }

    public async Task<OpenVpnServer> GetOpenVpnServer(int vpnServerId, CancellationToken cancellationToken)
    {
        return await _unitOfWork.GetQuery<OpenVpnServer>()
            .AsQueryable()
            .Where(x=> x.Id == vpnServerId)
            .OrderByDescending(x=>x.Id)
            .FirstOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("OpenVPN Server not found");
    }


    public async Task<OpenVpnServer> AddOpenVpnServer(OpenVpnServer openVpnServer, CancellationToken cancellationToken)
    {
        var openVpnServerRepository = _unitOfWork.GetRepository<OpenVpnServer>();
        await openVpnServerRepository.AddAsync(openVpnServer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (await CheckAndPutDefaultExpiredSettings(openVpnServer, cancellationToken))
        {
            _logger.LogWarning("Something went wrong when try to add OpenVPN Server settings");
        }
        
        return await _unitOfWork.GetQuery<OpenVpnServer>()
            .AsQueryable()
            .Where(x => x.Id == openVpnServer.Id)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken) ?? throw new InvalidOperationException();
    }
    
    public async Task<OpenVpnServer> UpdateOpenVpnServer(OpenVpnServer openVpnServer, CancellationToken cancellationToken)
    {
        var openVpnServerRepository = _unitOfWork.GetRepository<OpenVpnServer>();
        openVpnServerRepository.Update(openVpnServer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (await CheckAndPutDefaultExpiredSettings(openVpnServer, cancellationToken))
        {
            _logger.LogWarning("Something went wrong when try to add OpenVPN Server settings");
        }
        
        return await _unitOfWork.GetQuery<OpenVpnServer>()
            .AsQueryable()
            .Where(x => x.Id == openVpnServer.Id)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken) ?? throw new InvalidOperationException();
    }
    
    public async Task<bool> DeleteOpenVpnServer(int vpnServerId, CancellationToken cancellationToken)
    {
        var openVpnServerRepository = _unitOfWork.GetRepository<OpenVpnServer>();
        var openVpnServer = await openVpnServerRepository.GetByIdAsync(vpnServerId);
        openVpnServerRepository.Delete(openVpnServer ?? throw new InvalidOperationException("OpenVpnServer not found"));
        await _unitOfWork.SaveChangesAsync(cancellationToken); 
        return true;
    }

    private async Task<bool> CheckAndPutDefaultExpiredSettings(OpenVpnServer openVpnServer, CancellationToken cancellationToken)
    {
        var ovpnRepo = _unitOfWork.GetRepository<OpenVpnServerOvpnFileConfig>();
        if (!await ovpnRepo.Query
                .AnyAsync(x => x.VpnServerId == openVpnServer.Id, cancellationToken))
        {
            await ovpnRepo.AddAsync(new OpenVpnServerOvpnFileConfig
            {
                VpnServerId = openVpnServer.Id,
                VpnServerIp = await _externalIpAddressService.GetRemoteIpAddress(cancellationToken),
            }, cancellationToken);
        }

        var certRepo = _unitOfWork.GetRepository<OpenVpnServerCertConfig>();
        if (!await certRepo.Query
                .AnyAsync(x => x.VpnServerId == openVpnServer.Id, cancellationToken))
        {
            await certRepo.AddAsync(new OpenVpnServerCertConfig
            {
                VpnServerId = openVpnServer.Id,
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}