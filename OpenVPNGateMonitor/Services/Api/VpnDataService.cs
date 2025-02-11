using Microsoft.EntityFrameworkCore;
using OpenVPNGateMonitor.DataBase.UnitOfWork;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Helpers.Api;

namespace OpenVPNGateMonitor.Services.Api;

public class VpnDataService : IVpnDataService
{
    private readonly ILogger<IVpnDataService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    public VpnDataService(ILogger<IVpnDataService> logger, IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<OpenVpnServerClient>> GetAllConnectedOpenVpnServerClients(int vpnServerId, 
        CancellationToken cancellationToken)
    {
        var openVpnServerClients = await _unitOfWork.GetQuery<OpenVpnServerClient>()
            .AsQueryable().Where(x=> x.IsConnected && x.VpnServerId == vpnServerId)
            .OrderBy(x=>x.Id)
            .ToListAsync(cancellationToken);
        return openVpnServerClients;
    }
    public async Task<List<OpenVpnServerClient>> GetAllHistoryOpenVpnServerClients(int vpnServerId, 
        CancellationToken cancellationToken)
    {
        var openVpnServerClients = await _unitOfWork.GetQuery<OpenVpnServerClient>()
            .AsQueryable()
            .Where(x=> x.VpnServerId == vpnServerId)
            .OrderBy(x=>x.Id)
            .ToListAsync(cancellationToken);
        return openVpnServerClients;
    }

    public async Task<OpenVpnServerStatusLog?> GetOpenVpnServerStatusLog(int vpnServerId, 
        CancellationToken cancellationToken)
    {
        return await _unitOfWork.GetQuery<OpenVpnServerStatusLog>()
            .AsQueryable()
            .Where(x=> x.VpnServerId == vpnServerId)
            .OrderByDescending(x=>x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<OpenVpnServerInfoResponse>> GetAllOpenVpnServers(CancellationToken cancellationToken)
    {
        var openVpnServers = await _unitOfWork.GetQuery<OpenVpnServer>()
            .AsQueryable().OrderByDescending(x=>x.Id)
            .ToListAsync(cancellationToken: cancellationToken);
        
        var openVpnServerInfoResponses = new List<OpenVpnServerInfoResponse>();
        foreach (var openVpnServer in openVpnServers)
        {
            var openVpnServerStatusLog = await _unitOfWork.GetQuery<OpenVpnServerStatusLog>()
                .AsQueryable()
                .Where(x => x.VpnServerId == openVpnServer.Id)
                .OrderByDescending(x=>x.Id)
                .LastOrDefaultAsync(cancellationToken);
            
            openVpnServerInfoResponses.Add(new OpenVpnServerInfoResponse()
            {
                OpenVpnServer = openVpnServer,
                OpenVpnServerStatusLog = openVpnServerStatusLog
            });
        }
        return openVpnServerInfoResponses;
    }
    
    public async Task<OpenVpnServer> AddOpenVpnServer(OpenVpnServer openVpnServer, CancellationToken cancellationToken)
    {
        var openVpnServerRepository = _unitOfWork.GetRepository<OpenVpnServer>();
        await openVpnServerRepository.AddAsync(openVpnServer);
        await _unitOfWork.SaveChangesAsync(); 
        return openVpnServer;
    }
    
    public async Task<OpenVpnServer> UpdateOpenVpnServer(OpenVpnServer openVpnServer, CancellationToken cancellationToken)
    {
        var openVpnServerRepository = _unitOfWork.GetRepository<OpenVpnServer>();
        openVpnServerRepository.Update(openVpnServer);
        await _unitOfWork.SaveChangesAsync();
        return openVpnServer;
    }
    
    public async Task<OpenVpnServer> GetOpenVpnServer(int vpnServerId, CancellationToken cancellationToken)
    {
        var openVpnServerRepository = _unitOfWork.GetRepository<OpenVpnServer>();
        return await openVpnServerRepository.GetByIdAsync(vpnServerId) ?? throw new InvalidOperationException();
    }
    
    public async Task<OpenVpnServer> DeleteOpenVpnServer(int vpnServerId, CancellationToken cancellationToken)
    {
        var openVpnServerRepository = _unitOfWork.GetRepository<OpenVpnServer>();
        var openVpnServer = await openVpnServerRepository.GetByIdAsync(vpnServerId);
        openVpnServerRepository.Delete(openVpnServer ?? throw new InvalidOperationException("OpenVpnServer not found"));
        await _unitOfWork.SaveChangesAsync(); 
        return openVpnServer;
    }
}