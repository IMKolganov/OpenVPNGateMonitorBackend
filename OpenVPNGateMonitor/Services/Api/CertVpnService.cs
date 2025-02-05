using OpenVPNGateMonitor.DataBase.UnitOfWork;
using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Services.UntilsServices.Interfaces;

namespace OpenVPNGateMonitor.Services.Api;

public class CertVpnService : ICertVpnService
{
    private readonly ILogger<CertVpnService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEasyRsaService _easyRsaService;
    public CertVpnService(ILogger<CertVpnService> logger, IUnitOfWork unitOfWork, IEasyRsaService easyRsaService)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _easyRsaService = easyRsaService;
    }

    public List<CertificateCaInfo> GetAllVpnCertificates()
    {
        if(_easyRsaService.CheckHealthFileSystem()){
            throw new Exception("Something went wrong, some RSA directory could not be found");
        }
        
        return _easyRsaService.GetAllCertificateInfoInIndexFile();
    }
    
    public List<CertificateCaInfo> GetAllVpnCertificatesByStatus(CertificateStatus certificateStatus)
    {
        if(_easyRsaService.CheckHealthFileSystem()){
            throw new Exception("Something went wrong, some RSA directory could not be found");
        }
        
        return _easyRsaService.GetAllCertificateInfoInIndexFile()
            .Where(x=> x.Status == certificateStatus).ToList();
    }
}