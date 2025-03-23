using Mapster;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.SharedModels.Auth.Responses;

namespace OpenVPNGateMonitor.SharedModels.Auth.Mappings;

public class AuthMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ClientApplication, SystemSecretStatusResponse>()
            .Map(
                dest => dest.SystemSet, 
                src => !string.IsNullOrEmpty(src.ClientSecret)
                );
    }
}