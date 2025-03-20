using Mapster;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Models.Helpers.Services;
using OpenVPNGateMonitor.SharedModels.OpenVpnServers.Requests;
using OpenVPNGateMonitor.SharedModels.OpenVpnServers.Responses;

namespace OpenVPNGateMonitor.SharedModels.OpenVpnServers.Mappings;

public class VpnServerMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<OpenVpnServerClientList, ConnectedClientsResponse>()
            .Map(dest => dest.TotalCount, src => src.TotalCount)
            .Map(dest => dest.Clients, src => src.OpenVpnServerClients.Adapt<List<VpnClientInfoResponse>>());

        config.NewConfig<OpenVpnServerClient, VpnClientInfoResponse>();
        
        config.NewConfig<List<OpenVpnServerInfo>, ServersResponse>()
            .Map(dest => dest.Servers, src => src.Adapt<List<ServerInfoResponse>>());

        config.NewConfig<OpenVpnServerInfo, ServerInfoResponse>()
            .Map(dest => dest.ServerId, src => src.OpenVpnServer.Id)
            .Map(dest => dest.ServerName, src => src.OpenVpnServer.ServerName)
            .Map(dest => dest.IpAddress, src => src.OpenVpnServer.ManagementIp)
            .Map(dest => dest.Port, src => src.OpenVpnServer.ManagementPort)
            .Map(dest => dest.IsOnline, src => src.OpenVpnServer.IsOnline)
            .Map(dest => dest.Status, src => src.OpenVpnServerStatusLog != null ? "Online" : "Offline")
            .Map(dest => dest.TotalBytesIn, src => src.OpenVpnServerStatusLog != null ? src.OpenVpnServerStatusLog.BytesIn : 0)
            .Map(dest => dest.TotalBytesOut, src => src.OpenVpnServerStatusLog != null ? src.OpenVpnServerStatusLog.BytesOut : 0)
            .Map(dest => dest.Version, src => src.OpenVpnServerStatusLog != null ? src.OpenVpnServerStatusLog.Version : "Unknown")
            .Map(dest => dest.UpSince, src => src.OpenVpnServerStatusLog!.UpSince);
        
        config.NewConfig<OpenVpnServer, ServerResponse>()
            .Map(dest => dest.ServerId, src => src.Id)
            .Map(dest => dest.ServerName, src => src.ServerName)
            .Map(dest => dest.ManagementIp, src => src.ManagementIp)
            .Map(dest => dest.ManagementPort, src => src.ManagementPort)
            .Map(dest => dest.IsOnline, src => src.IsOnline);

        config.NewConfig<AddServerRequest, OpenVpnServer>()
            .Map(dest => dest.ServerName, src => src.ServerName)
            .Map(dest => dest.ManagementIp, src => src.ManagementIp)
            .Map(dest => dest.ManagementPort, src => src.ManagementPort)
            .Map(dest => dest.IsOnline, src => src.IsOnline);

        config.NewConfig<UpdateServerRequest, OpenVpnServer>()
            .Map(dest => dest.Id, src => src.ServerId)
            .Map(dest => dest.ServerName, src => src.ServerName)
            .Map(dest => dest.ManagementIp, src => src.ManagementIp)
            .Map(dest => dest.ManagementPort, src => src.ManagementPort)
            .Map(dest => dest.IsOnline, src => src.IsOnline);
        
        config.NewConfig<KeyValuePair<string, ServiceStatus>, ServiceStatusResponse>()
            .Map(dest => dest.ServiceName, src => src.Key)
            .Map(dest => dest.Status, src => src.Value.ToString());
    }
}