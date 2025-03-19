using Mapster;
using OpenVPNGateMonitor.SharedModels.OpenVpnServerOvpnFileConfig.Requests;
using OpenVPNGateMonitor.SharedModels.OpenVpnServerOvpnFileConfig.Responses;

namespace OpenVPNGateMonitor.SharedModels.OpenVpnServerOvpnFileConfig.Mappings;

public class OvpnFileConfigMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Models.OpenVpnServerOvpnFileConfig, OvpnFileConfigResponse>();

        config.NewConfig<AddOrUpdateOvpnFileConfigRequest, Models.OpenVpnServerOvpnFileConfig>();
    }
}