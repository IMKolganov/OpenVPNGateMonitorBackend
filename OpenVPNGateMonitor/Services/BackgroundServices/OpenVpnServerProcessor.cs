using OpenVPNGateMonitor.DataBase.UnitOfWork;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

namespace OpenVPNGateMonitor.Services.BackgroundServices;

public class OpenVpnServerProcessor
{
    private readonly ILogger<OpenVpnServerProcessor> _logger;
    private readonly ICommandQueueManager _commandQueueManager;
    private readonly IServiceProvider _serviceProvider;

    public OpenVpnServerProcessor(
        ILogger<OpenVpnServerProcessor> logger,
        IServiceProvider serviceProvider,
        ICommandQueueManager commandQueueManager)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _commandQueueManager = commandQueueManager;
    }

    public async Task ProcessServerAsync(OpenVpnServer openVpnServer, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Processing OpenVPN server: {openVpnServer.ManagementIp}:{openVpnServer.ManagementPort}");
        using var scope = _serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var openVpnServerService = scope.ServiceProvider.GetRequiredService<IOpenVpnServerService>();
        var openVpnServerRepository = unitOfWork.GetRepository<OpenVpnServer>();
        try
        {
            var commandQueue = await _commandQueueManager.GetOrCreateQueueAsync(
                openVpnServer.ManagementIp, openVpnServer.ManagementPort, cancellationToken, 5);//todo: load from config

            _logger.LogInformation($"Saving OpenVPN server status for {openVpnServer.ManagementIp}:{openVpnServer.ManagementPort}");
            await openVpnServerService.SaveOpenVpnServerStatusLogAsync(openVpnServer.Id, commandQueue, cancellationToken);

            _logger.LogInformation($"Saving connected clients for {openVpnServer.ManagementIp}:{openVpnServer.ManagementPort}");
            await openVpnServerService.SaveConnectedClientsAsync(openVpnServer.Id, commandQueue, cancellationToken);

            openVpnServer.IsOnline = true;
            openVpnServerRepository.Update(openVpnServer);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation($"Finished processing OpenVPN server: {openVpnServer.ManagementIp}:{openVpnServer.ManagementPort}");
        }
        catch (Exception ex)
        {
            openVpnServer.IsOnline = false;
            openVpnServerRepository.Update(openVpnServer);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogError(ex, $"OpenVpnServerProcessor: Error processing OpenVPN server {openVpnServer.ManagementIp}:{openVpnServer.ManagementPort}");
            throw;
        }
    }
}