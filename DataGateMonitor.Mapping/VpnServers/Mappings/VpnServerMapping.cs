using Mapster;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Responses;

namespace DataGateMonitor.Mapping.VpnServers.Mappings;

public class VpnServerMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // Entity ↔ DTO: DB column / model is IsDisable; API contract uses IsDisabled.
        config.NewConfig<VpnServer, VpnServerDto>()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.ServerType, src => src.ServerType)
            .Map(dest => dest.ServerName, src => src.ServerName)
            .Map(dest => dest.IsOnline, src => src.IsOnline)
            .Map(dest => dest.IsDefault, src => src.IsDefault)
            .Map(dest => dest.ApiUrl, src => src.ApiUrl)
            .Map(dest => dest.Latitude, src => src.Latitude)
            .Map(dest => dest.Longitude, src => src.Longitude)
            .Map(dest => dest.IsEnableWss, src => src.IsEnableWss)
            .Map(dest => dest.IsPiHoleEnabled, src => src.IsPiHoleEnabled)
            .Map(dest => dest.CreateDate, src => src.CreateDate)
            .Map(dest => dest.LastUpdate, src => src.LastUpdate)
            .Map(dest => dest.IsDeleted, src => src.IsDeleted)
            .Map(dest => dest.DcoIsEnabled, src => src.DcoIsEnabled)
            .Map(dest => dest.XrayClientsPolledAt, src => src.XrayClientsPolledAt)
            .Map(dest => dest.XrayClientsPollError, src => src.XrayClientsPollError)
            .Map(dest => dest.IsDisabled, src => src.IsDisable)
            .Map(dest => dest.GroupId, src => src.VpnServerGroupId)
            .Map(dest => dest.SortOrder, src => src.SortOrder)
            .Ignore(dest => dest.GroupName)
            .Ignore(dest => dest.Tags);

        config.NewConfig<UpdateServerRequest, VpnServer>()
            .Map(dest => dest.IsDisable, src => src.IsDisabled);

        config.NewConfig<AddServerRequest, VpnServer>()
            .Map(dest => dest.IsDisable, src => src.IsDisabled);

        config.NewConfig<List<VpnServer>, VpnServersResponse>()
            .Map(dest => dest.VpnServers, src => src);

        config.NewConfig<VpnServerDto, VpnServerV2Dto>()
            .Ignore(dest => dest.QuotaPlanGroups)
            .Ignore(dest => dest.IsAccessibleForUserQuotaPlan);

        config.NewConfig<VpnServer, VpnServerResponse>()
            .Map(d => d.VpnServer, s => s);

        config.NewConfig<VpnServerStatusLog, VpnServerStatusLogResponse>();

        config.NewConfig<VpnServerWithStatusDto, VpnServerWithStatusResponse>()
            .Map(d => d.VpnServerWithStatus, s => s);

        config.NewConfig<List<VpnServerWithStatusDto>, VpnServerWithStatusesResponse>()
            .Map(d => d.VpnServerWithStatuses, s => s);

        TypeAdapterConfig<ServiceStatusDto, ServiceStatusResponse>
            .NewConfig()
            .Map(dest => dest.ServiceStatus.VpnServerId, src => src.VpnServerId)
            .Map(dest => dest.ServiceStatus.Status, src => src.Status)
            .Map(dest => dest.ServiceStatus.CountConnectedClients, src => src.CountConnectedClients)
            .Map(dest => dest.ServiceStatus.CountSessions, src => src.CountSessions)
            .Map(dest => dest.ServiceStatus.ErrorMessage, src => src.ErrorMessage)
            .Map(dest => dest.ServiceStatus.NextRunTime, src => src.NextRunTime);
    }
}
