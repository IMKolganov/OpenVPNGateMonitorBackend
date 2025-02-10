using Microsoft.EntityFrameworkCore;
using OpenVPNGateMonitor.DataBase.UnitOfWork;
using OpenVPNGateMonitor.Models;

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

    public async Task<List<OpenVpnServerClient>> GetAllConnectedOpenVpnServerClients(CancellationToken cancellationToken)
    {
        var openVpnServerClients = await _unitOfWork.GetQuery<OpenVpnServerClient>()
            .AsQueryable().Where(x=> x.IsConnected).OrderBy(x=>x.Id)
            .ToListAsync(cancellationToken);
        return openVpnServerClients;
    }
    public async Task<List<OpenVpnServerClient>> GetAllHistoryOpenVpnServerClients(CancellationToken cancellationToken)
    {
        var openVpnServerClients = await _unitOfWork.GetQuery<OpenVpnServerClient>()
            .AsQueryable().OrderBy(x=>x.Id)
            .ToListAsync(cancellationToken);
        return openVpnServerClients;
    }

    public async Task<OpenVpnServerStatusLog?> GetOpenVpnServerStatusLog(CancellationToken cancellationToken)
    {
        return await _unitOfWork.GetQuery<OpenVpnServerStatusLog>()
            .AsQueryable()
            .OrderByDescending(x=>x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<OpenVpnServer>> GetAllOpenVpnServers(CancellationToken cancellationToken)
    {
        return await _unitOfWork.GetQuery<OpenVpnServer>()
            .AsQueryable().OrderByDescending(x=>x.Id)
            .ToListAsync(cancellationToken: cancellationToken);
    }
    
    public async Task<OpenVpnServer> AddOpenVpnServer(OpenVpnServer openVpnServer, CancellationToken cancellationToken)
    {
        var openVpnServerRepository = _unitOfWork.GetRepository<OpenVpnServer>();
        await openVpnServerRepository.AddAsync(openVpnServer);
        await _unitOfWork.SaveChangesAsync(); 
        return openVpnServer;
    }
    
    public async Task<OpenVpnServer> DeleteOpenVpnServer(OpenVpnServer openVpnServer, CancellationToken cancellationToken)
    {
        var openVpnServerRepository = _unitOfWork.GetRepository<OpenVpnServer>();
        openVpnServerRepository.Delete(openVpnServer);
        await _unitOfWork.SaveChangesAsync(); 
        return openVpnServer;
    }
}