using Microsoft.EntityFrameworkCore;
using OpenVPNGateMonitor.DataBase.UnitOfWork;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Services.UntilsServices.Interfaces;

namespace OpenVPNGateMonitor.Services.Api;

public class CertVpnService : ICertVpnService
{
    private readonly ILogger<ICertVpnService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEasyRsaService _easyRsaService;
    public CertVpnService(ILogger<ICertVpnService> logger, IUnitOfWork unitOfWork, IEasyRsaService easyRsaService)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _easyRsaService = easyRsaService;
    }

    public async Task<List<CertificateCaInfo>> GetAllVpnCertificates(int vpnServerId, 
        CancellationToken cancellationToken)
    {
        var openVpnServerCertConfig = await GetOpenVpnServerCertConf(vpnServerId, cancellationToken);

        if (openVpnServerCertConfig == null) throw new InvalidOperationException();
        if(!_easyRsaService.CheckHealthFileSystem(openVpnServerCertConfig)){
            throw new Exception("Something went wrong, some RSA directory could not be found");
        }
        
        return _easyRsaService.GetAllCertificateInfoInIndexFile(openVpnServerCertConfig.PkiPath);
    }
    
    public async Task<List<CertificateCaInfo>> GetAllVpnCertificatesByStatus(int vpnServerId,
        CertificateStatus certificateStatus, CancellationToken cancellationToken)
    {
        var openVpnServerCertConfig = await GetOpenVpnServerCertConf(vpnServerId, cancellationToken);
        
        if(!_easyRsaService.CheckHealthFileSystem(openVpnServerCertConfig)){
            throw new Exception("Something went wrong, some RSA directory could not be found");
        }
        
        return _easyRsaService.GetAllCertificateInfoInIndexFile(openVpnServerCertConfig.PkiPath)
            .Where(x=> x.Status == certificateStatus).ToList();
    }

    public async Task<CertificateBuildResult> AddCertificate(int vpnServerId, string cnName, 
        CancellationToken cancellationToken)
    {
        var openVpnServerCertConfig = await GetOpenVpnServerCertConf(vpnServerId, cancellationToken);
        //first realization, with "nopass", without any params if you need more check method BuildCertificate
        return _easyRsaService.BuildCertificate(openVpnServerCertConfig, cnName);
    }
    
    public async Task<CertificateRevokeResult> RemoveCertificate(int vpnServerId, string cnName, 
        CancellationToken cancellationToken)
    {
        var openVpnServerCertConfig = await GetOpenVpnServerCertConf(vpnServerId, cancellationToken);
        return _easyRsaService.RevokeCertificate(openVpnServerCertConfig, cnName);
    }

    private async Task<OpenVpnServerCertConfig> GetOpenVpnServerCertConf(int vpnServerId, 
        CancellationToken cancellationToken)
    {
        return await _unitOfWork.GetQuery<OpenVpnServerCertConfig>()
            .AsQueryable()
            .Where(x => x.VpnServerId == vpnServerId)
            .FirstOrDefaultAsync(cancellationToken) ?? 
               throw new InvalidOperationException("OpenVpnServerCertConfig not found");
    }
    
}