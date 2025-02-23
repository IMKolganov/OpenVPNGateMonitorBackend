using OpenVPNGateMonitor.Models.Enums;

namespace OpenVPNGateMonitor.Models.Helpers.Api;

public class BackgroundServiceStatus
{
    private ServiceStatus _serviceStatus = ServiceStatus.Idle;

    public ServiceStatus ServiceStatus
    {
        get => _serviceStatus;
        set => _serviceStatus = SetServiceStatus(value);
    }
    public string ErrorMessage { get; set; } = string.Empty;

    private ServiceStatus SetServiceStatus(ServiceStatus serviceStatus = ServiceStatus.Idle)
    {
        if (serviceStatus != ServiceStatus.Error)
        {
            ErrorMessage = string.Empty;
        }
        return serviceStatus;
    }
}
