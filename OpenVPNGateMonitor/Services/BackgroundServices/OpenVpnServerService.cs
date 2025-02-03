using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OpenVPNGateMonitor.DataBase.UnitOfWork;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Helpers.Api;
using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

namespace OpenVPNGateMonitor.Services.BackgroundServices;

public class OpenVpnServerService : IOpenVpnServerService
{
    private readonly ILogger<IOpenVpnServerService> _logger;
    private readonly IOpenVpnClientService _openVpnClientService;
    private readonly IOpenVpnSummaryStatService _openVpnSummaryStatService;
    private readonly IOpenVpnVersionService _openVpnVersionService;
    private readonly IOpenVpnStateService _openVpnStateService;
    private readonly IUnitOfWork _unitOfWork;
    
    public OpenVpnServerService(ILogger<IOpenVpnServerService> logger, IOpenVpnClientService openVpnClientService,
        IOpenVpnSummaryStatService openVpnSummaryStatService, IOpenVpnVersionService openVpnVersionService,
        IOpenVpnStateService openVpnStateService, IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _openVpnClientService = openVpnClientService;
        _openVpnSummaryStatService = openVpnSummaryStatService;
        _openVpnVersionService = openVpnVersionService;
        _openVpnStateService = openVpnStateService;
        _unitOfWork = unitOfWork;
    }

    public async Task SaveConnectedClientsAsync(CancellationToken cancellationToken)
    {
        var openVpnClients = await _openVpnClientService.GetClientsAsync(cancellationToken);
        var openVpnServerClientRepository = _unitOfWork.GetRepository<OpenVpnServerClient>();
        
        var existingClients = await _unitOfWork.GetQuery<OpenVpnServerClient>()
            .AsQueryable().Where(x=> x.IsConnected)
            .ToListAsync(cancellationToken);
        foreach (var client in existingClients)
        {
            client.IsConnected = false;
        }
        await _unitOfWork.SaveChangesAsync();

        foreach (var openVpnClient in openVpnClients)
        {
            var sessionId = GenerateSessionId(openVpnClient.CommonName,
                openVpnClient.RemoteIp, openVpnClient.ConnectedSince);

            var existingClient = await _unitOfWork.GetQuery<OpenVpnServerClient>()
                .AsQueryable()
                .FirstOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);

            if (existingClient != null)
            {
                existingClient.BytesReceived = openVpnClient.BytesReceived;
                existingClient.BytesSent = openVpnClient.BytesSent;
                existingClient.LastUpdate = DateTime.UtcNow;
                existingClient.Country = openVpnClient.Country;
                existingClient.Region = openVpnClient.Region;
                existingClient.City = openVpnClient.City;
                existingClient.Latitude = openVpnClient.Latitude;
                existingClient.Longitude = openVpnClient.Longitude;
                existingClient.IsConnected = true;

                openVpnServerClientRepository.Update(existingClient);
            }
            else
            {
                var newClient = new OpenVpnServerClient()
                {
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

                await openVpnServerClientRepository.AddAsync(newClient);
            }
        }
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task SaveOpenVpnServerStatusLogAsync(CancellationToken cancellationToken)
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get OpenVPN Summary Stats");
        }

        serverInfo.Status = serverInfo.OpenVpnState != null ? "CONNECTED" : "DISCONNECTED";

        var openVpnServerStatusLogRepository = _unitOfWork.GetRepository<OpenVpnServerStatusLog>();

        if (serverInfo.OpenVpnState == null)
        {
            throw new InvalidOperationException("OpenVPN State is null. Cannot proceed.");
        }

        var sessionId = GenerateSessionId(
            "RaspberryVPN", // TODO: make server name
            serverInfo.OpenVpnState.LocalIp ?? throw new InvalidOperationException("LocalIp cannot be null"),
            serverInfo.OpenVpnState.UpSince
        );

        var existingStatusLog = await _unitOfWork.GetQuery<OpenVpnServerStatusLog>()
            .AsQueryable()
            .FirstOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);

        if (existingStatusLog != null)
        {
            existingStatusLog.BytesIn = serverInfo.OpenVpnSummaryStats?.BytesIn ?? 0;
            existingStatusLog.BytesOut = serverInfo.OpenVpnSummaryStats?.BytesOut ?? 0;
            existingStatusLog.Version = serverInfo.Version;
            existingStatusLog.LastUpdate = DateTime.UtcNow;

            openVpnServerStatusLogRepository.Update(existingStatusLog);
        }
        else
        {
            var newStatusLog = new OpenVpnServerStatusLog
            {
                SessionId = sessionId,
                UpSince = serverInfo.OpenVpnState.UpSince,
                LocalIp = serverInfo.OpenVpnState.LocalIp,
                RemoteIp = serverInfo.OpenVpnState.RemoteIp ?? string.Empty,
                BytesIn = serverInfo.OpenVpnSummaryStats?.BytesIn ?? 0,
                BytesOut = serverInfo.OpenVpnSummaryStats?.BytesOut ?? 0,
                Version = serverInfo.Version,
                LastUpdate = DateTime.UtcNow,
                CreateDate = DateTime.UtcNow
            };

            await openVpnServerStatusLogRepository.AddAsync(newStatusLog);
        }

        await _unitOfWork.SaveChangesAsync();
    }
    
    private Guid GenerateSessionId(string commonName, string realAddress, DateTime connectedSince)
    {
        var sessionString = $"{commonName}-{realAddress}-{connectedSince:o}";
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sessionString));
        return new Guid(hashBytes.Take(16).ToArray());
    }
}