namespace OpenVPNGateMonitor.Services.Helpers;

public class ExternalIpAddressService
{
    private readonly ILogger<ExternalIpAddressService> _logger;
    private readonly List<string>? _externalIpServices;
    
    public  ExternalIpAddressService(ILogger<ExternalIpAddressService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _externalIpServices = configuration.GetSection("ExternalIpServices").Get<List<string>>();
    }
    
    public async Task<string> GetRemoteIpAddress(CancellationToken cancellationToken)
    {
        using HttpClient client = new();

        if (_externalIpServices is not { Count: > 0 })
        {
            _logger.LogError("No external IP services configured.");
            return "127.0.0.1";
        }

        foreach (string service in _externalIpServices)
        {
            try
            {
                string ip = await client.GetStringAsync(service, cancellationToken);
                _logger.LogInformation("Retrieved external IP: {Ip} from {Service}", ip.Trim(), service);
                return ip.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get IP from {Service}", service);
            }
        }

        _logger.LogError("Unable to retrieve external IP from any configured service.");
        return "127.0.0.1";
    }
}