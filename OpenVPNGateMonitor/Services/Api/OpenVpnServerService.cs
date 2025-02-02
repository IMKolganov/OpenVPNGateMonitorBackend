using OpenVPNGateMonitor.Models.Helpers.Api;
using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;
using OpenVPNGateMonitor.Services.Api.Interfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

namespace OpenVPNGateMonitor.Services.Api;

public class OpenVpnServerService : IOpenVpnServerService
{
    private readonly ILogger<IOpenVpnServerService> _logger;
    private readonly IOpenVpnClientService _openVpnClientService;
    private readonly IOpenVpnSummaryStatService _openVpnSummaryStatService;
    private readonly IOpenVpnVersionService _openVpnVersionService;
    private readonly IOpenVpnStateService _openVpnStateService;


    public OpenVpnServerService(ILogger<IOpenVpnServerService> logger, IOpenVpnClientService openVpnClientService,
        IOpenVpnSummaryStatService openVpnSummaryStatService, IOpenVpnVersionService openVpnVersionService,
        IOpenVpnStateService openVpnStateService)
    {
        _logger = logger;
        _openVpnClientService = openVpnClientService;
        _openVpnSummaryStatService = openVpnSummaryStatService;
        _openVpnVersionService = openVpnVersionService;
        _openVpnStateService = openVpnStateService;
    }

    public async Task<List<OpenVpnClient>> GetConnectedClientsAsync(CancellationToken cancellationToken)
    {
        var openVpnClients = await _openVpnClientService.GetClientsAsync(cancellationToken);
        return openVpnClients;
    }
    
    public async Task<ServerInfo> GetServerInfoAsync(CancellationToken cancellationToken)
    {
        var serverInfo = new ServerInfo();
        try
        {
            serverInfo.OpenVpnState = await _openVpnStateService.GetStateAsync(cancellationToken);
            serverInfo.OpenVpnSummaryStats = await _openVpnSummaryStatService.GetSummaryStatsAsync(cancellationToken);
            if (serverInfo.OpenVpnState != null)
            {
                serverInfo.Version = await _openVpnVersionService.GetVersionAsync(cancellationToken);
            }
        }
        catch
        {
            _logger.LogError("Failed to get OpenVPN Summary Stats");
        }
        serverInfo.Status = serverInfo.OpenVpnState != null ?  "CONNECTED" : "DISCONNECTED";
        
        return serverInfo;
    }
}