namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

public interface IOpenVpnVersionService
{
    Task<string> GetVersionAsync(string managementIp, int managementPort, CancellationToken cancellationToken);
}