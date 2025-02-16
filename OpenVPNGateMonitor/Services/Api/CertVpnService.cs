using Microsoft.EntityFrameworkCore;
using OpenVPNGateMonitor.DataBase.UnitOfWork;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Models.Helpers.Api;
using OpenVPNGateMonitor.Services.Api.Interfaces;
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

    public async Task<List<CertificateCaInfo>> GetAllVpnServerCertificates(int vpnServerId, 
        CancellationToken cancellationToken)
    {
        var openVpnServerCertConfig = await GetOpenVpnServerCertConf(vpnServerId, cancellationToken);

        if (openVpnServerCertConfig == null) throw new InvalidOperationException();
        if(!_easyRsaService.CheckHealthFileSystem(openVpnServerCertConfig)){
            throw new Exception("Something went wrong, some RSA directory could not be found");
        }
        
        return _easyRsaService.GetAllCertificateInfoInIndexFile(openVpnServerCertConfig.PkiPath);
    }
    
    public async Task<List<CertificateCaInfo>> GetAllVpnServerCertificatesByStatus(int vpnServerId,
        CertificateStatus certificateStatus, CancellationToken cancellationToken)
    {
        var openVpnServerCertConfig = await GetOpenVpnServerCertConf(vpnServerId, cancellationToken);
        
        if(!_easyRsaService.CheckHealthFileSystem(openVpnServerCertConfig)){
            throw new Exception("Something went wrong, some RSA directory could not be found");
        }
        
        return _easyRsaService.GetAllCertificateInfoInIndexFile(openVpnServerCertConfig.PkiPath)
            .Where(x=> x.Status == certificateStatus).ToList();
    }

    public async Task<CertificateBuildResult> AddServerCertificate(int vpnServerId, string cnName, 
        CancellationToken cancellationToken)
    {
        var openVpnServerCertConfig = await GetOpenVpnServerCertConf(vpnServerId, cancellationToken);
        //first realization, with "nopass", without any params if you need more check method BuildCertificate
        return _easyRsaService.BuildCertificate(openVpnServerCertConfig, cnName);
    }
    
    public async Task<CertificateRevokeResult> RevokeServerCertificate(int vpnServerId, string cnName, 
        CancellationToken cancellationToken)
    {
        var openVpnServerCertConfig = await GetOpenVpnServerCertConf(vpnServerId, cancellationToken);
        return _easyRsaService.RevokeCertificate(openVpnServerCertConfig, cnName);
    }

    public async Task<OpenVpnServerCertConfig> GetOpenVpnServerCertConf(int vpnServerId, 
        CancellationToken cancellationToken)
    {
        return await _unitOfWork.GetQuery<OpenVpnServerCertConfig>()
            .AsQueryable()
            .Where(x => x.VpnServerId == vpnServerId)
            .FirstOrDefaultAsync(cancellationToken) ?? 
               throw new InvalidOperationException("OpenVpnServerCertConfig not found");
    }

    public async Task<OpenVpnServerCertConfig> UpdateServerCertConfig(
        OpenVpnServerCertConfigRequest openVpnServerCertConfigRequest,
        CancellationToken cancellationToken)
    {
        var repositoryOpenVpnServerCertConfig = _unitOfWork.GetRepository<OpenVpnServerCertConfig>();

        var exstOpenVpnServerCertConfig = await _unitOfWork.GetQuery<OpenVpnServerCertConfig>()
            .AsQueryable()
            .Where(x => x.VpnServerId == openVpnServerCertConfigRequest.VpnServerId)
            .FirstOrDefaultAsync(cancellationToken);

        OpenVpnServerCertConfig resultConfig;

        if (exstOpenVpnServerCertConfig != null)
        {
            exstOpenVpnServerCertConfig.EasyRsaPath = openVpnServerCertConfigRequest.EasyRsaPath;
            exstOpenVpnServerCertConfig.OvpnFileDir = openVpnServerCertConfigRequest.OvpnFileDir;
            exstOpenVpnServerCertConfig.RevokedOvpnFilesDirPath =
                openVpnServerCertConfigRequest.RevokedOvpnFilesDirPath;
            exstOpenVpnServerCertConfig.PkiPath = openVpnServerCertConfigRequest.PkiPath;
            exstOpenVpnServerCertConfig.CaCertPath = openVpnServerCertConfigRequest.CaCertPath;
            exstOpenVpnServerCertConfig.TlsAuthKey = openVpnServerCertConfigRequest.TlsAuthKey;
            exstOpenVpnServerCertConfig.ServerRemoteIp = openVpnServerCertConfigRequest.ServerRemoteIp;
            exstOpenVpnServerCertConfig.CrlPkiPath = openVpnServerCertConfigRequest.CrlPkiPath;
            exstOpenVpnServerCertConfig.CrlOpenvpnPath = openVpnServerCertConfigRequest.CrlOpenvpnPath;
            exstOpenVpnServerCertConfig.StatusFilePath = openVpnServerCertConfigRequest.StatusFilePath;

            repositoryOpenVpnServerCertConfig.Update(exstOpenVpnServerCertConfig);
            resultConfig = exstOpenVpnServerCertConfig;
        }
        else
        {
            resultConfig = new OpenVpnServerCertConfig
            {
                VpnServerId = openVpnServerCertConfigRequest.VpnServerId,
                EasyRsaPath = openVpnServerCertConfigRequest.EasyRsaPath,
                OvpnFileDir = openVpnServerCertConfigRequest.OvpnFileDir,
                RevokedOvpnFilesDirPath = openVpnServerCertConfigRequest.RevokedOvpnFilesDirPath,
                PkiPath = openVpnServerCertConfigRequest.PkiPath,
                CaCertPath = openVpnServerCertConfigRequest.CaCertPath,
                TlsAuthKey = openVpnServerCertConfigRequest.TlsAuthKey,
                ServerRemoteIp = openVpnServerCertConfigRequest.ServerRemoteIp,
                CrlPkiPath = openVpnServerCertConfigRequest.CrlPkiPath,
                CrlOpenvpnPath = openVpnServerCertConfigRequest.CrlOpenvpnPath,
                StatusFilePath = openVpnServerCertConfigRequest.StatusFilePath
            };
            await repositoryOpenVpnServerCertConfig.AddAsync(resultConfig);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return resultConfig;
    }
}