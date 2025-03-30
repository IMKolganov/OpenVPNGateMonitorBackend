using Microsoft.EntityFrameworkCore;
using OpenVPNGateMonitor.DataBase.UnitOfWork;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Services.Api.Interfaces;

namespace OpenVPNGateMonitor.Services.Api;

public class OpenVpnServerOvpnFileConfigService : IOpenVpnServerOvpnFileConfigService
{
    private readonly ILogger<OpenVpnServerOvpnFileConfigService> _logger;
    private readonly IUnitOfWork _unitOfWork;

    
    public OpenVpnServerOvpnFileConfigService(ILogger<OpenVpnServerOvpnFileConfigService> logger, 
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
    }
    
    public async Task<OpenVpnServerOvpnFileConfig> GetOpenVpnServerOvpnFileConfigByServerId(int vpnServerId, 
        CancellationToken cancellationToken)
    {
        
        return await _unitOfWork.GetQuery<OpenVpnServerOvpnFileConfig>()
            .AsQueryable()
            .Where(x => x.VpnServerId == vpnServerId)
            .FirstOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("OvpnFileConfig not found");
    }
    
    public async Task<OpenVpnServerOvpnFileConfig> AddOrUpdateOpenVpnServerOvpnFileConfigByServerId(
        OpenVpnServerOvpnFileConfig openVpnServerOvpnFileConfig, CancellationToken cancellationToken)
    {
        var openVpnServerOvpnFileConfigRepository = _unitOfWork.GetRepository<OpenVpnServerOvpnFileConfig>();

        var existingConfig = await _unitOfWork.GetQuery<OpenVpnServerOvpnFileConfig>()
            .AsQueryable()
            .FirstOrDefaultAsync(x => x.VpnServerId == openVpnServerOvpnFileConfig.VpnServerId, 
                cancellationToken);

        if (existingConfig != null)
        {
            existingConfig.VpnServerIp = openVpnServerOvpnFileConfig.VpnServerIp;
            existingConfig.VpnServerPort = openVpnServerOvpnFileConfig.VpnServerPort;
            existingConfig.ConfigTemplate = openVpnServerOvpnFileConfig.ConfigTemplate;
            existingConfig.LastUpdate = DateTime.UtcNow;

            openVpnServerOvpnFileConfigRepository.Update(existingConfig);
        }
        else
        {
            openVpnServerOvpnFileConfig.CreateDate = DateTime.UtcNow;
            openVpnServerOvpnFileConfig.LastUpdate = DateTime.UtcNow;

            await openVpnServerOvpnFileConfigRepository.AddAsync(openVpnServerOvpnFileConfig, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await _unitOfWork.GetQuery<OpenVpnServerOvpnFileConfig>()
                   .AsQueryable()
                   .FirstOrDefaultAsync(x => x.VpnServerId == openVpnServerOvpnFileConfig.VpnServerId,
                       cancellationToken)
               ?? throw new InvalidOperationException($"OpenVPN server OVPN file configuration not found for " +
                                                      $"server ID {openVpnServerOvpnFileConfig.VpnServerId}.");
    }

    
}