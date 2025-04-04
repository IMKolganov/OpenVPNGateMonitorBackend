﻿using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Helpers.Api;
using OpenVPNGateMonitor.Models.Helpers.Services;

namespace OpenVPNGateMonitor.Services.Api.Interfaces;

public interface IVpnDataService
{
    Task<OpenVpnServerClientList> GetAllConnectedOpenVpnServerClients(
        int vpnServerId, int page, int pageSize, CancellationToken cancellationToken);
    Task<OpenVpnServerClientList> GetAllHistoryOpenVpnServerClients(
        int vpnServerId, int page, int pageSize, CancellationToken cancellationToken);
    Task<List<OpenVpnServerWithStatus>> GetAllOpenVpnServersWithStatus(CancellationToken cancellationToken);
    Task<OpenVpnServerWithStatus> GetOpenVpnServerWithStatus(int vpnServerId, CancellationToken cancellationToken);
    Task<OpenVpnServer> GetOpenVpnServer(int vpnServerId, CancellationToken cancellationToken);
    Task<OpenVpnServer> AddOpenVpnServer(OpenVpnServer openVpnServer, CancellationToken cancellationToken);
    Task<OpenVpnServer> UpdateOpenVpnServer(OpenVpnServer openVpnServer, CancellationToken cancellationToken);
    Task<bool> DeleteOpenVpnServer(int vpnServerId, CancellationToken cancellationToken);
}