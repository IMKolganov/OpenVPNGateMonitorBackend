using System.Diagnostics;
using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Services.UntilsServices.Interfaces;

namespace OpenVPNGateMonitor.Services.UntilsServices;

public class EasyRsaService : IEasyRsaService
{
    private readonly ILogger<IEasyRsaService> _logger;
    private readonly IEasyRsaParseDbService _easyRsaParseDbService;
    private readonly IEasyRsaExecCommandService _easyRsaExecCommandService;
    private readonly string _pkiPath;
    private readonly OpenVpnSettings _openVpnSettings;
    private readonly string _caCertPath;
    private readonly string _revokedDirPath;


    public EasyRsaService(ILogger<IEasyRsaService> logger, IConfiguration configuration, 
        IEasyRsaParseDbService easyRsaParseDbService, IEasyRsaExecCommandService easyRsaExecCommandService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _easyRsaParseDbService = easyRsaParseDbService;
        _easyRsaExecCommandService = easyRsaExecCommandService;

        var openVpnSection = configuration.GetSection("OpenVpn");
        if (!openVpnSection.Exists())
        {
            throw new InvalidOperationException("OpenVpn section is missing in the configuration.");
        }

        _openVpnSettings = openVpnSection.Get<OpenVpnSettings>()
                           ?? throw new InvalidOperationException("Failed to load OpenVpnSettings from configuration.");
        
        _logger.LogInformation("Loaded OpenVpnSettings: EasyRsaPath: {EasyRsaPath}, OutputDir: {OutputDir}, TlsAuthKey: {TlsAuthKey}, ServerIp: {ServerIp}", 
            _openVpnSettings.EasyRsaPath, 
            _openVpnSettings.OutputDir, 
            _openVpnSettings.TlsAuthKey, 
            _openVpnSettings.ServerIp);

        if (string.IsNullOrEmpty(_openVpnSettings.EasyRsaPath))
        {
            throw new InvalidOperationException("OpenVpnSettings: EasyRsaPath is missing or empty.");
        }

        if (string.IsNullOrEmpty(_openVpnSettings.OutputDir))
        {
            throw new InvalidOperationException("OpenVpnSettings: OutputDir is missing or empty.");
        }

        if (string.IsNullOrEmpty(_openVpnSettings.TlsAuthKey))
        {
            throw new InvalidOperationException("OpenVpnSettings: TlsAuthKey is missing or empty.");
        }

        if (string.IsNullOrEmpty(_openVpnSettings.ServerIp))
        {
            throw new InvalidOperationException("OpenVpnSettings: ServerIp is missing or empty.");
        }

        _pkiPath = Path.Combine(_openVpnSettings.EasyRsaPath, "pki");
        _caCertPath = Path.Combine(_pkiPath, "ca.crt");
        _revokedDirPath = Path.Combine(_openVpnSettings.OutputDir, "revoked");
        _logger.LogInformation($"PKI path initialized to: {_pkiPath}");
    }

    #region easyrsa build-client-full
// # ==============================================================================
// # EasyRSA build-client-full command variations
// # ==============================================================================
//
// # | Command Example                                      | Description                                      |
// # |------------------------------------------------------|--------------------------------------------------|
// # | ./easyrsa build-client-full client1                 | Creates a client certificate with a password prompt |
// # | ./easyrsa build-client-full client1 nopass          | Creates a client certificate without a password |
// # | EASYRSA_BATCH=1 ./easyrsa build-client-full client1 nopass  | Skips confirmation prompts during execution |
// # | EASYRSA_CERT_EXPIRE=3650 ./easyrsa build-client-full client1 nopass | Sets the certificate expiration to 10 years (3650 days) |
// # | EASYRSA_KEY_SIZE=4096 ./easyrsa build-client-full client1 nopass | Generates a 4096-bit key instead of the default 2048-bit |
// # | EASYRSA_DIGEST="sha512" ./easyrsa build-client-full client1 nopass | Uses SHA-512 hashing instead of the default SHA-256 |
// # | EASYRSA_ALGO="ec" EASYRSA_CURVE="secp384r1" ./easyrsa build-client-full client1 nopass | Uses ECDSA instead of RSA with secp384r1 curve |
// # | EASYRSA_PASSIN="mypassword" ./easyrsa build-client-full client1 | Automatically provides the password instead of prompting |
// # | EASYRSA_PASSIN=file:/path/to/password.txt ./easyrsa build-client-full client1 | Reads the password from a file |
// # ==============================================================================

    #endregion
    public CertificateBuildResult BuildCertificate(string baseFileName = "client1")
    {
        var command = $"cd {_openVpnSettings.EasyRsaPath} && ./easyrsa build-client-full {baseFileName} nopass";
        var (output, error, exitCode) = _easyRsaExecCommandService.RunCommand(command);

        if (exitCode != 0)
        {
            throw new Exception($"Error while building certificate: {error}");
        }
        _logger.LogInformation($"Certificate generated successfully:\n{output}");

        var certPath = Path.Combine(_pkiPath, "issued", $"{baseFileName}.crt");
        var certificateInfoInIndexFile = GetAllCertificateInfoInIndexFile()
            .Where(x=> x.Status == CertificateStatus.Active && x.CommonName == baseFileName).ToList();
        if (certificateInfoInIndexFile.Count <= 0)
        {
            throw new Exception($"Error certificate is not found in CA {certPath}");
        }
        if (!certificateInfoInIndexFile.FirstOrDefault()!.SerialNumber.Contains(CheckCertInOpenssl(certPath)))
        {
            throw new Exception($"Certificate serial number " +
                                $"{certificateInfoInIndexFile.FirstOrDefault()!.SerialNumber} is invalid.");
        }
        var pemSerialPath = Path.Combine(_pkiPath, "certs_by_serial", 
            $"{certificateInfoInIndexFile.FirstOrDefault()!.SerialNumber}.pem");

        _logger.LogInformation($"Certificate path: {pemSerialPath}");
        return new CertificateBuildResult
        {
            CertificatePath = certPath,
            KeyPath = Path.Combine(_pkiPath, "private", $"{baseFileName}.key"),
            RequestPath = Path.Combine(_pkiPath, "reqs", $"{baseFileName}.req"),
            PemPath = pemSerialPath,
            CertId = certificateInfoInIndexFile.FirstOrDefault()!.SerialNumber
        };
    }

    private string CheckCertInOpenssl(string certPath)
    {
        var certPathCommand = $"openssl x509 -in {certPath} -serial -noout";
        var (certOutput, certError, certExitCode) = _easyRsaExecCommandService.RunCommand(certPathCommand);

        if (certExitCode != 0)
        {
            throw new Exception($"Error occurred while retrieving certificate serial: {certError}");
        }

        var serial = certOutput.Split('=')[1].Trim();
        _logger.LogInformation($"Certificate serial retrieved:\n{serial} Full response: \n{certOutput}");
        return serial;
    }


    public string ReadPemContent(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        return string.Join(Environment.NewLine, lines
            .SkipWhile(line => !line.StartsWith("-----BEGIN CERTIFICATE-----"))
            .TakeWhile(line => !line.StartsWith("-----END CERTIFICATE-----"))
            .Append("-----END CERTIFICATE-----"));
    }

    public CertificateRevokeResult RevokeCertificate(string cnName)
    {
        var certificateRevokeResult = new CertificateRevokeResult();
        certificateRevokeResult.CertificatePath = Path.Combine(_pkiPath, "issued", $"{cnName}.crt");
        if (!File.Exists(certificateRevokeResult.CertificatePath))
        {
            _logger.LogInformation($"EasyRsa path: {_openVpnSettings.EasyRsaPath}");
            _logger.LogInformation($"PKI path: {_pkiPath}");
            throw new Exception($"Certificate file not found: {certificateRevokeResult.CertificatePath}");
        }

        _logger.LogInformation($"Attempting to revoke certificate for: {cnName}");
        _logger.LogInformation($"EasyRsaPath: {_openVpnSettings.EasyRsaPath}");
        _logger.LogInformation($"PKI Path: {_pkiPath}");
        _logger.LogInformation($"Certificate Path: {certificateRevokeResult.CertificatePath}");

        // Revoke the certificate
        var revokeResult = _easyRsaExecCommandService.ExecuteEasyRsaCommand($"revoke {cnName}", 
            _openVpnSettings.EasyRsaPath, confirm: true);
        certificateRevokeResult.IsRevoked = revokeResult.IsSuccess;
        if (!certificateRevokeResult.IsRevoked)
        {
            switch (revokeResult.ExitCode)
            {
                case 0:
                    certificateRevokeResult.Message += $"Certificate revoked successfully: {cnName}";
                    _logger.LogInformation($"Certificate revoked successfully: {cnName}");
                    break;

                case 1:
                    if (revokeResult.Output.Contains("ERROR:Already revoked") 
                        || revokeResult.Error.Contains("ERROR:Already revoked"))
                    {
                        certificateRevokeResult.Message += $"Certificate is already revoked: {cnName}";
                        _logger.LogWarning($"Certificate is already revoked: {cnName}");
                    }
                    else if (revokeResult.Output.Contains("ERROR: Certificate not found") 
                             || revokeResult.Output.Contains("ERROR: Certificate not found"))
                    {
                        certificateRevokeResult.Message += $"Certificate not found: {cnName}";
                        _logger.LogWarning($"Certificate not found: {cnName}");
                    }
                    else
                    {
                        throw new Exception($"Failed to revoke certificate. Unknown error: {cnName}, " +
                                            $"ExitCode: {revokeResult.ExitCode}, Output: {revokeResult.Output}");
                    }
                    break;

                default:
                    throw new Exception($"Unexpected exit code ({revokeResult.ExitCode}) " +
                                        $"while revoking certificate: {cnName}");
            }
        }

        _logger.LogInformation("Revocation successful. Generating CRL...");
        if (UpdateCrl())
            _logger.LogInformation("CRL successfully updated and deployed.");

        _logger.LogInformation("Certificate successfully revoked, CRL updated and deployed.");
        return certificateRevokeResult;
    }

    private bool UpdateCrl()
    {
        var crlResult = _easyRsaExecCommandService.ExecuteEasyRsaCommand("gen-crl", 
            _openVpnSettings.EasyRsaPath);
        if (!crlResult.IsSuccess)
        {
            _logger.LogInformation($"Command Output: {crlResult.Output}");
            throw new Exception($"Failed to generate CRL: {crlResult.Error}");
        }
        
        if (!File.Exists(_openVpnSettings.CrlPkiPath))
        {
            throw new Exception($"Generated CRL not found at {_openVpnSettings.CrlPkiPath}," +
                                $" Command Output: {crlResult.Output}");
        }
        
        try
        {
            // Copy the CRL to the OpenVPN directory
            string copyCommand = $"cp {_openVpnSettings.CrlPkiPath} {_openVpnSettings.CrlOpenvpnPath}";
            var copyResult = _easyRsaExecCommandService.RunCommand(copyCommand);

            if (copyResult.ExitCode != 0)
            {
                _logger.LogInformation($"Command Output: {copyResult.Output}");
                throw new Exception($"Failed to copy CRL file: {copyResult.Error}");
            }

            _logger.LogInformation($"copyResult - {copyResult}");
            _logger.LogInformation($"CRL copied to {_openVpnSettings.CrlOpenvpnPath}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Error during CRL update: {ex.Message}");
        }

        return true;
    }
    
    public List<CertificateCaInfo> GetAllCertificateInfoInIndexFile()
    {
        return _easyRsaParseDbService.ParseCertificateInfoInIndexFile(_pkiPath);
    }
    
    public bool CheckHealthFileSystem()
    {
        InstallEasyRsa();
        if (string.IsNullOrEmpty(_openVpnSettings.ServerIp))
            throw new ArgumentNullException(nameof(_openVpnSettings.ServerIp));
        
        Directory.CreateDirectory(_openVpnSettings.OutputDir);
        Directory.CreateDirectory(_revokedDirPath);
        
        if (!Directory.Exists(_openVpnSettings.OutputDir))
        {
            throw new FileNotFoundException("The output directory could not be found.");
        }
        if (!Directory.Exists(_revokedDirPath))
        {
            throw new FileNotFoundException("Revoked folder not found");
        }
        
        string indexFilePath = Path.Combine(_pkiPath, "index.txt");
        if (!File.Exists(indexFilePath))
        {
            throw new FileNotFoundException($"Index file not found at path: {indexFilePath}");
        }

        if (string.IsNullOrEmpty(_caCertPath))
            throw new ArgumentNullException(nameof(_caCertPath));
        if (string.IsNullOrEmpty(_openVpnSettings.TlsAuthKey))
            throw new ArgumentNullException(nameof(_openVpnSettings.TlsAuthKey));

        return true;
    }
    
    private void InstallEasyRsa()
    {
        if (!Directory.Exists(_pkiPath))
        {
            _logger.LogInformation("PKI directory does not exist. Initializing PKI...");
            _easyRsaExecCommandService.RunCommand($"cd {_openVpnSettings.EasyRsaPath} && ./easyrsa init-pki");
            throw new Exception("PKI directory does not exist.");
        }
        else
        {
            _logger.LogInformation("PKI directory exists. Skipping initialization...");
        }
    }
}