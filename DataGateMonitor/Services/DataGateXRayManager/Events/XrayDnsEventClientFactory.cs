using System.Collections.Concurrent;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Services.DataGateXRayManager.Events;

public interface IXrayDnsEventClientFactory
{
    XrayDnsEventClient Create(VpnServer server);
}

public sealed class XrayDnsEventClientFactory(IServiceProvider rootProvider) : IXrayDnsEventClientFactory
{
    private readonly ConcurrentDictionary<int, XrayDnsEventClient> _cache = new();

    public XrayDnsEventClient Create(VpnServer server)
    {
        if (server.ServerType != VpnServerType.Xray)
            throw new InvalidOperationException($"Xray DNS event client requires Xray server (got {server.ServerType}).");

        var normalized = (server.ApiUrl ?? "").TrimEnd('/');
        return _cache.AddOrUpdate(
            server.Id,
            _ => CreateNew(server),
            (key, existing) =>
            {
                if (!string.Equals(existing.RegisteredApiUrl.TrimEnd('/'), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    _ = existing.StopAsync();
                    return CreateNew(server);
                }
                return existing;
            });
    }

    private XrayDnsEventClient CreateNew(VpnServer server)
    {
        return new XrayDnsEventClient(
            server,
            rootProvider.GetRequiredService<ILogger<XrayDnsEventClient>>(),
            rootProvider.GetRequiredService<IMicroserviceTokenService>(),
            rootProvider.GetRequiredService<IServiceScopeFactory>());
    }
}
