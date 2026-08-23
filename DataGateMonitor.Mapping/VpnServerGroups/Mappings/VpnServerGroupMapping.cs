using Mapster;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerGroups.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerGroups.Responses;

namespace DataGateMonitor.Mapping.VpnServerGroups.Mappings;

public class VpnServerGroupMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<VpnServerGroup, VpnServerGroupDto>()
            .Ignore(dest => dest.ServerIds);
        config.NewConfig<VpnServerGroup, VpnServerGroupResponse>()
            .Map(dest => dest.Group, src => src);
        config.NewConfig<List<VpnServerGroup>, VpnServerGroupsResponse>()
            .Map(dest => dest.Groups, src => src.Adapt<List<VpnServerGroupDto>>());
    }
}
