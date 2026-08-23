using Mapster;
using DataGateMonitor.Mapping.Applications.Mappings;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.DataGateMonitor.Applications.Dto;
using Xunit;

namespace DataGateMonitor.Tests.Mapping.Applications;

public class ApplicationMappingTests
{
    public ApplicationMappingTests()
    {
        var config = TypeAdapterConfig.GlobalSettings;
        new ApplicationMapping().Register(config);
    }

    [Fact]
    public void ClientApplication_To_ApplicationDto_Does_Not_Expose_ClientSecret()
    {
        var entity = new ClientApplication
        {
            ClientId = "cid",
            Name = "Bot",
            ClientSecret = "$2a$11$should-never-leak",
            IsRevoked = false,
            IsSystem = true,
        };

        var dto = entity.Adapt<ApplicationDto>();

        Assert.Equal("cid", dto.ClientId);
        Assert.Equal("Bot", dto.Name);
        Assert.True(dto.IsSystem);
        Assert.True(string.IsNullOrEmpty(dto.ClientSecret));
    }
}
