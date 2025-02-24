using OpenVPNGateMonitor.DataBase.UnitOfWork;
using OpenVPNGateMonitor.Models;

namespace OpenVPNGateMonitor.Services.BackgroundServices;

public class OpenVpnServerProcessorFactory
{
    private readonly Dictionary<int, OpenVpnServerProcessor> _processors = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly object _lock = new();

    public OpenVpnServerProcessorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public OpenVpnServerProcessor GetOrCreateProcessor(OpenVpnServer server)
    {
        lock (_lock)
        {
            if (!_processors.TryGetValue(server.Id, out var processor))
            {
                processor = ActivatorUtilities.CreateInstance<OpenVpnServerProcessor>(_serviceProvider);
                _processors[server.Id] = processor;
            }

            return processor;
        }
    }
}
