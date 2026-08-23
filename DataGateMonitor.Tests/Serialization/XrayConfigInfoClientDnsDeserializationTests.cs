using DataGateMonitor.Serialization;
using DataGateMonitor.SharedModels.DataGateXRayManager.Info;

namespace DataGateMonitor.Tests.Serialization;

/// <summary>
/// Backend <c>MicroserviceInfoService</c> deserializes node <c>/api/info</c> via <see cref="ProjectJson"/>.
/// Ensure ClientDnsServers / DnsIdentityEnabled round-trip for dashboard diagnostics.
/// </summary>
public class XrayConfigInfoClientDnsDeserializationTests
{
    [Fact]
    public void Deserialize_RootXrayInfo_MapsClientDnsServersAndIdentityFlag()
    {
        const string json =
            """
            {
              "version": "1.1.2.22",
              "environment": "Production",
              "application": "DataGateXRayManager",
              "description": "test",
              "publicIp": "203.0.113.10",
              "config": {
                "dns1": "172.20.0.1",
                "dns2": "8.8.4.4",
                "clientDnsServers": ["172.20.0.1", "8.8.4.4"],
                "dnsIdentityEnabled": true,
                "port": "443",
                "apiPort": "5010"
              }
            }
            """;

        var info = ProjectJson.Deserialize<RootXrayInfoResponse>(json);
        Assert.NotNull(info);
        Assert.Equal("DataGateXRayManager", info!.Application);
        Assert.Equal("172.20.0.1", info.Config.Dns1);
        Assert.True(info.Config.DnsIdentityEnabled);
        Assert.Equal(["172.20.0.1", "8.8.4.4"], info.Config.ClientDnsServers);
    }

    [Fact]
    public void Deserialize_MissingClientDnsServers_DefaultsToEmptyList()
    {
        const string json =
            """
            {
              "application": "DataGateXRayManager",
              "config": {
                "dns1": "172.20.0.1",
                "dnsIdentityEnabled": true,
                "port": "443"
              }
            }
            """;

        var info = ProjectJson.Deserialize<RootXrayInfoResponse>(json);
        Assert.NotNull(info);
        Assert.NotNull(info!.Config.ClientDnsServers);
        Assert.Empty(info.Config.ClientDnsServers);
        Assert.True(info.Config.DnsIdentityEnabled);
        Assert.Equal("172.20.0.1", info.Config.Dns1);
    }

    [Fact]
    public void Deserialize_EmptyClientDnsServersArray_StaysEmpty()
    {
        const string json =
            """
            {
              "application": "DataGateXRayManager",
              "config": {
                "clientDnsServers": [],
                "dnsIdentityEnabled": true
              }
            }
            """;

        var info = ProjectJson.Deserialize<RootXrayInfoResponse>(json);
        Assert.Empty(info!.Config.ClientDnsServers);
        Assert.True(info.Config.DnsIdentityEnabled);
    }

    [Fact]
    public void Serialize_ConfigInfo_EmitsCamelCaseClientDnsFields()
    {
        var json = ProjectJson.Serialize(new ConfigInfoResponse
        {
            Dns1 = "172.20.0.1",
            ClientDnsServers = ["172.20.0.1"],
            DnsIdentityEnabled = true
        });

        Assert.Contains("\"clientDnsServers\"", json);
        Assert.Contains("\"dnsIdentityEnabled\":true", json);
        Assert.Contains("172.20.0.1", json);
    }
}
