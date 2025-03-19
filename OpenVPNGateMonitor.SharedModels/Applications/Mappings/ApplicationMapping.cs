using Mapster;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.SharedModels.Applications.Requests;
using OpenVPNGateMonitor.SharedModels.Applications.Responses;

namespace OpenVPNGateMonitor.SharedModels.Applications.Mappings;

public class ApplicationMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ClientApplication, ApplicationDto>();
        config.NewConfig<RegisterApplicationRequest, ClientApplication>();
    }
}