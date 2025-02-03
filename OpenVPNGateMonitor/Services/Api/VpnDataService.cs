using Microsoft.EntityFrameworkCore;
using OpenVPNGateMonitor.DataBase.UnitOfWork;
using OpenVPNGateMonitor.Models;

namespace OpenVPNGateMonitor.Services.Api;

public class VpnDataService : IVpnDataService
{
    private readonly ILogger<VpnDataService> _logger;
    private readonly IUnitOfWork _unitOfWork;

    
    public VpnDataService(ILogger<VpnDataService> logger, IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<OpenVpnServerClient>> GetAllConnectedOpenVpnServerClients(CancellationToken cancellationToken)
    {
        var openVpnServerClients = await _unitOfWork.GetQuery<OpenVpnServerClient>()
            .AsQueryable().Where(x=> x.IsConnected)
            .ToListAsync(cancellationToken);
        return openVpnServerClients;
    }
    
    public async Task<OpenVpnServerStatusLog?> GetOpenVpnServerStatusLog(CancellationToken cancellationToken)
    {
        return await _unitOfWork.GetQuery<OpenVpnServerStatusLog>()
            .AsQueryable()
            .OrderByDescending(x => x.CreateDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

}