using System.Net;
using System.Net.Sockets;
using DataGateMonitor.Services.Helpers.Interfaces;

namespace DataGateMonitor.Services.Helpers;

public class ExternalIpAddressService(
    ILogger<ExternalIpAddressService> logger,
    IConfiguration configuration,
    HttpClient httpClient)
    : IExternalIpAddressService
{
    private readonly List<string>? _externalIpServices = configuration
        .GetSection("ExternalIpServices")
        .Get<List<string>>();

    public async Task<string> GetRemoteIpAddress(CancellationToken cancellationToken)
    {
        if (_externalIpServices is not { Count: > 0 })
        {
            logger.LogError("No external IP services configured.");
            return "127.0.0.1";
        }

        foreach (var service in _externalIpServices)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
                using var request = new HttpRequestMessage(HttpMethod.Get, service);
                request.Headers.Accept.ParseAdd("text/plain");
                using var response = await httpClient.SendAsync(request, timeoutCts.Token);
                response.EnsureSuccessStatusCode();
                var raw = (await response.Content.ReadAsStringAsync(timeoutCts.Token)).Trim();
                if (!TryParsePublicIp(raw, out var ip))
                {
                    logger.LogWarning("Ignoring non-IP response from {Service}", service);
                    continue;
                }

                logger.LogInformation("Retrieved external IP: {Ip} from {Service}", ip, service);
                return ip;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Failed to get IP from {Service}", service);
            }
        }

        logger.LogError("Unable to retrieve external IP from any configured service.");
        return "127.0.0.1";
    }

    internal static bool TryParsePublicIp(string? raw, out string ip)
    {
        ip = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var candidate = raw.Split(['\r', '\n', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries)[0];
        if (!IPAddress.TryParse(candidate, out var address))
            return false;

        if (address.AddressFamily is not (AddressFamily.InterNetwork or AddressFamily.InterNetworkV6))
            return false;

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
            return false;

        ip = address.ToString();
        return true;
    }
}
