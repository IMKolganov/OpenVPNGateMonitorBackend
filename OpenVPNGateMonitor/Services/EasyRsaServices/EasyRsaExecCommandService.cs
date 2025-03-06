using System.Diagnostics;
using OpenVPNGateMonitor.Services.EasyRsaServices.Interfaces;

namespace OpenVPNGateMonitor.Services.EasyRsaServices;

public class EasyRsaExecCommandService : IEasyRsaExecCommandService
{
    private readonly ILogger<IEasyRsaExecCommandService> _logger;

    public EasyRsaExecCommandService(ILogger<IEasyRsaExecCommandService> logger)
    {
        _logger = logger;
    }

    #region EasyRSA revoke command variations
// # ==============================================================================
// # EasyRSA revoke command variations
// # ==============================================================================
//
// # | Command Example                                    | Description                                        |
// # |----------------------------------------------------|----------------------------------------------------|
// # | ./easyrsa revoke client1                          | Revokes the client certificate (client1)          |
// # | EASYRSA_BATCH=1 ./easyrsa revoke client1          | Revokes the certificate without confirmation prompt |
// # | EASYRSA_CRL_DAYS=3650 ./easyrsa revoke client1    | Sets the Certificate Revocation List (CRL) expiration to 10 years |
// # | ./easyrsa gen-crl                                 | Generates or updates the Certificate Revocation List (CRL) |
// # | EASYRSA_CRL_DAYS=7300 ./easyrsa gen-crl          | Generates a CRL valid for 20 years                |
//
// # ==============================================================================

    #endregion
    public (bool IsSuccess, string Output, int ExitCode, string Error) ExecuteEasyRsaCommand(string arguments, 
        string easyRsaPath, bool confirm = false)
    {
        try
        {
            var command = $"cd {easyRsaPath} && ./easyrsa {arguments}";
            if (confirm)
            {
                _logger.LogInformation($"Confirming command with 'yes': {arguments}");
                command = $"cd {easyRsaPath} && echo yes | ./easyrsa {arguments}";
            }

            _logger.LogInformation($"Executing command: {command}");
            var result = RunCommand(command);

            _logger.LogInformation($"Command Output: {result.Output}");
            _logger.LogInformation($"Command Error: {result.Error}");
            _logger.LogInformation($"Command Exit Code: {result.ExitCode}");

            return result.ExitCode == 0
                ? (true, result.Output, result.ExitCode, string.Empty)
                : (false, result.Output, result.ExitCode, result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Exception during command execution: {ex.Message}");
            return (false, string.Empty, 404,ex.Message);
        }
    }

    public (string Output, string Error, int ExitCode) RunCommand(string command)
    {
        var processInfo = new ProcessStartInfo("bash", $"-c \"{command}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _logger.LogInformation($"Starting process: {command}");
        using var process = Process.Start(processInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start command process.");
        }

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        _logger.LogInformation($"Process completed with ExitCode: {process.ExitCode}, Error: {error}, Output: {output}");
        return (output, error, process.ExitCode);
    }
}