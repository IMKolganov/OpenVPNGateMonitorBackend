namespace OpenVPNGateMonitor.Services.EasyRsaServices.Interfaces;

public interface ICommandRunner
{
    (string Output, string Error, int ExitCode) RunCommand(string command);
}