using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OpenVPNGateMonitor.DataBase.UnitOfWork;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Helpers.Api;
using OpenVPNGateMonitor.Models.Helpers.Services;
using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;
using OpenVPNGateMonitor.Services.Helpers;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

namespace OpenVPNGateMonitor.Services.BackgroundServices;

public class OpenVpnServerService : IOpenVpnServerService
{
    private readonly ILogger<IOpenVpnServerService> _logger;
    private readonly IOpenVpnClientService _openVpnClientService;
    private readonly IOpenVpnSummaryStatService _openVpnSummaryStatService;
    private readonly IOpenVpnVersionService _openVpnVersionService;
    private readonly IOpenVpnStateService _openVpnStateService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ExternalIpAddressService _externalIpAddressService;
    
    public OpenVpnServerService(ILogger<IOpenVpnServerService> logger, IOpenVpnClientService openVpnClientService, 
        IOpenVpnSummaryStatService openVpnSummaryStatService, IOpenVpnVersionService openVpnVersionService, 
        IOpenVpnStateService openVpnStateService, IUnitOfWork unitOfWork, ExternalIpAddressService externalIpAddressService)
    {
        _logger = logger;
        _openVpnClientService = openVpnClientService;
        _openVpnSummaryStatService = openVpnSummaryStatService;
        _openVpnVersionService = openVpnVersionService;
        _openVpnStateService = openVpnStateService;
        _unitOfWork = unitOfWork;
        _externalIpAddressService = externalIpAddressService;

        _logger.LogInformation("OpenVpnServerService initialized.");
    }

    public async Task SaveConnectedClientsAsync(int vpnServerId, ICommandQueue commandQueue,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting SaveConnectedClientsAsync...");

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var openVpnClients = await _openVpnClientService.GetClientsAsync(commandQueue, cancellationToken);
            _logger.LogInformation("Retrieved {Count} clients from OpenVPN.", openVpnClients.Count);

            await SetDisconnectForAllUsers(vpnServerId, cancellationToken);

            var openVpnServerClientRepository = _unitOfWork.GetRepository<OpenVpnServerClient>();

            foreach (var openVpnClient in openVpnClients)
            {
                var sessionId = GenerateSessionId(openVpnClient.CommonName,
                    openVpnClient.RemoteIp, openVpnClient.ConnectedSince);

                var existingOpenVpnServerClient = await _unitOfWork.GetQuery<OpenVpnServerClient>()
                    .AsQueryable()
                    .FirstOrDefaultAsync(x =>
                            x.SessionId == sessionId && x.VpnServerId == vpnServerId
                        , cancellationToken);

                if (existingOpenVpnServerClient != null)
                {
                    existingOpenVpnServerClient.VpnServerId = existingOpenVpnServerClient.VpnServerId;
                    existingOpenVpnServerClient.BytesReceived = openVpnClient.BytesReceived;
                    existingOpenVpnServerClient.BytesSent = openVpnClient.BytesSent;
                    existingOpenVpnServerClient.LastUpdate = DateTime.UtcNow;
                    existingOpenVpnServerClient.Country = openVpnClient.Country;
                    existingOpenVpnServerClient.Region = openVpnClient.Region;
                    existingOpenVpnServerClient.City = openVpnClient.City;
                    existingOpenVpnServerClient.Latitude = openVpnClient.Latitude;
                    existingOpenVpnServerClient.Longitude = openVpnClient.Longitude;
                    existingOpenVpnServerClient.IsConnected = true;

                    openVpnServerClientRepository.Update(existingOpenVpnServerClient);
                    _logger.LogDebug("Updated client session {SessionId}.", sessionId);
                }
                else
                {
                    var newClient = new OpenVpnServerClient()
                    {
                        VpnServerId = vpnServerId,
                        SessionId = sessionId,
                        CommonName = openVpnClient.CommonName,
                        RemoteIp = openVpnClient.RemoteIp,
                        LocalIp = openVpnClient.LocalIp,
                        BytesReceived = openVpnClient.BytesReceived,
                        BytesSent = openVpnClient.BytesSent,
                        ConnectedSince = openVpnClient.ConnectedSince,
                        Username = openVpnClient.Username,
                        Country = openVpnClient.Country,
                        Region = openVpnClient.Region,
                        City = openVpnClient.City,
                        Latitude = openVpnClient.Latitude,
                        Longitude = openVpnClient.Longitude,
                        IsConnected = true,
                        LastUpdate = DateTime.UtcNow,
                        CreateDate = DateTime.UtcNow
                    };

                    await openVpnServerClientRepository.AddAsync(newClient, cancellationToken);
                    _logger.LogInformation("Added new client session {SessionId}.", sessionId);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("SaveConnectedClientsAsync completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred in SaveConnectedClientsAsync, rolling back transaction.");
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task SaveOpenVpnServerStatusLogAsync(int vpnServerId, ICommandQueue commandQueue,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting SaveOpenVpnServerStatusLogAsync...");

        var serverInfo = new ServerInfo();
        try
        {
            serverInfo.OpenVpnState = await _openVpnStateService.GetStateAsync(commandQueue, cancellationToken);
            if (serverInfo.OpenVpnState.UpSince <= DateTime.MinValue)
            {
                throw new Exception("UpSince is not set. Check your configuration or server.");
            }
            
            serverInfo.OpenVpnSummaryStats = await _openVpnSummaryStatService.GetSummaryStatsAsync(commandQueue, 
                cancellationToken);
            serverInfo.OpenVpnState.ServerRemoteIp = await _externalIpAddressService.GetRemoteIpAddress(cancellationToken);

            if (serverInfo.OpenVpnState != null)
            {
                serverInfo.Version = await _openVpnVersionService.GetVersionAsync(commandQueue, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get OpenVPN Summary Stats. Error: {ex.Message}");
            throw;
        }

        serverInfo.Status = serverInfo.OpenVpnState != null ? "CONNECTED" : "DISCONNECTED";

        var openVpnServerStatusLogRepository = _unitOfWork.GetRepository<OpenVpnServerStatusLog>();

        if (serverInfo.OpenVpnState == null)
        {
            _logger.LogWarning("OpenVPN State is null. Cannot proceed.");
            throw new InvalidOperationException("OpenVPN State is null. Cannot proceed.");
        }

        var sessionId = GenerateSessionId(
            "RaspberryVPN", // TODO: make server name
            serverInfo.OpenVpnState.ServerLocalIp ?? 
            throw new InvalidOperationException("LocalIp cannot be null"),
            serverInfo.OpenVpnState.UpSince
        );

        var existingStatusLog = await _unitOfWork.GetQuery<OpenVpnServerStatusLog>()
            .AsQueryable()
            .FirstOrDefaultAsync(x => 
                x.SessionId == sessionId && x.VpnServerId == vpnServerId,
                cancellationToken);

        if (existingStatusLog != null)
        {
            existingStatusLog.VpnServerId = vpnServerId;
            existingStatusLog.UpSince = serverInfo.OpenVpnState.UpSince;
            existingStatusLog.ServerLocalIp = serverInfo.OpenVpnState.ServerLocalIp;
            existingStatusLog.ServerRemoteIp = serverInfo.OpenVpnState.ServerRemoteIp;
            existingStatusLog.BytesIn = serverInfo.OpenVpnSummaryStats?.BytesIn ?? 0;
            existingStatusLog.BytesOut = serverInfo.OpenVpnSummaryStats?.BytesOut ?? 0;
            existingStatusLog.Version = serverInfo.Version;
            existingStatusLog.LastUpdate = DateTime.UtcNow;

            openVpnServerStatusLogRepository.Update(existingStatusLog);
            _logger.LogInformation("Updated existing status log {SessionId}.", sessionId);
        }
        else
        {
            var newStatusLog = new OpenVpnServerStatusLog
            {
                VpnServerId = vpnServerId,
                SessionId = sessionId,
                UpSince = serverInfo.OpenVpnState.UpSince,
                ServerLocalIp = serverInfo.OpenVpnState.ServerLocalIp,
                ServerRemoteIp = serverInfo.OpenVpnState.ServerRemoteIp,
                BytesIn = serverInfo.OpenVpnSummaryStats?.BytesIn ?? 0,
                BytesOut = serverInfo.OpenVpnSummaryStats?.BytesOut ?? 0,
                Version = serverInfo.Version,
                LastUpdate = DateTime.UtcNow,
                CreateDate = DateTime.UtcNow
            };

            await openVpnServerStatusLogRepository.AddAsync(newStatusLog, cancellationToken);
            _logger.LogInformation("Created new status log {SessionId}.", sessionId);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("SaveOpenVpnServerStatusLogAsync completed successfully.");
    }
    
    private Guid GenerateSessionId(string commonName, string realAddress, DateTime connectedSince)
    {
        _logger.LogDebug("Generating SessionId for CommonName: {CommonName}, RealAddress: {RealAddress}, ConnectedSince: {ConnectedSince}", 
            commonName, realAddress, connectedSince);

        var sessionString = $"{commonName}-{realAddress}-{connectedSince:o}";
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sessionString));
    
        var sessionId = new Guid(hashBytes.Take(16).ToArray());
        _logger.LogDebug("Generated SessionId: {SessionId}", sessionId);

        return sessionId;
    }

    private async Task<bool> SetDisconnectForAllUsers(int vpnServerId, CancellationToken cancellationToken)
    {
        var existingAllOpenVpnServerClient = await _unitOfWork.GetQuery<OpenVpnServerClient>()
            .AsQueryable().Where(x => x.IsConnected && x.VpnServerId == vpnServerId)
            .ToListAsync(cancellationToken);

        foreach (var client in existingAllOpenVpnServerClient)
        {
            client.IsConnected = false;
        }
        _logger.LogInformation("Marked {Count} existing clients as disconnected.", existingAllOpenVpnServerClient.Count);
        return true;
    }
}
