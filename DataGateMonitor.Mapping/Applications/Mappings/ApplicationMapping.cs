using Mapster;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.DataGateMonitor.Applications.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.Applications.Responses;

namespace DataGateMonitor.Mapping.Applications.Mappings;

public class ApplicationMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        #region "get-all"
        config.NewConfig<ClientApplication, ApplicationDto>()
            .Map(d => d.ClientId, s => s.ClientId)
            .Map(d => d.Name, s => s.Name)
            .Map(d => d.IsRevoked, s => s.IsRevoked)
            .Map(d => d.IsSystem, s => s.IsSystem)
            .Map(d => d.CreateDate, s => s.CreateDate)
            .Map(d => d.LastUpdate, s => s.LastUpdate)
            .Ignore(d => d.ClientSecret);

        config.NewConfig<List<ClientApplication>, ApplicationsResponse>()
            .Map(d => d.Applications, s => s);
        #endregion

        #region "register"
        config.NewConfig<ClientApplication, RegisterApplicationResponse>()
            .Map(d => d.Name, s => s.Name)
            .Map(d => d.ClientId, s => s.ClientId)
            .Map(d => d.ClientSecret, s => s.ClientSecret);
        #endregion
    }
}