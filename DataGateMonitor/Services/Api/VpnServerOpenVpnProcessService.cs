using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.Services.Api.Interfaces;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.OpenVpnProcess.Responses;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Services.Api;

public class VpnServerOpenVpnProcessService(
    IVpnServerQueryService vpnServerQueryService,
    IHttpClientFactory httpClientFactory,
    IMicroserviceTokenService tokenService,
    ILogger<VpnServerOpenVpnProcessService> logger) : IVpnServerOpenVpnProcessService
{
    private const string AudienceOpenVpnManager = "DataGateOpenVpnManager";

    internal const string BusyMessage =
        "An OpenVPN process operation is already in progress for this server. Please wait and try again.";

    private static readonly ConcurrentDictionary<int, SemaphoreSlim> ServerGates = new();

    public Task<OpenVpnProcessStatusResponse> GetStatusAsync(int vpnServerId, CancellationToken ct) =>
        SendAsync(vpnServerId, HttpMethod.Get, "api/openvpn/status", exclusive: false, ct);

    public Task<OpenVpnProcessStatusResponse> StartAsync(int vpnServerId, CancellationToken ct) =>
        SendAsync(vpnServerId, HttpMethod.Post, "api/openvpn/start", exclusive: true, ct);

    public Task<OpenVpnProcessStatusResponse> RestartAsync(int vpnServerId, CancellationToken ct) =>
        SendAsync(vpnServerId, HttpMethod.Post, "api/openvpn/restart", exclusive: true, ct);

    public Task<OpenVpnProcessStatusResponse> KillAsync(int vpnServerId, CancellationToken ct) =>
        SendAsync(vpnServerId, HttpMethod.Post, "api/openvpn/kill", exclusive: true, ct);

    private async Task<OpenVpnProcessStatusResponse> SendAsync(
        int vpnServerId,
        HttpMethod method,
        string relativePath,
        bool exclusive,
        CancellationToken ct)
    {
        SemaphoreSlim? gate = null;
        if (exclusive)
        {
            gate = ServerGates.GetOrAdd(vpnServerId, _ => new SemaphoreSlim(1, 1));
            if (!await gate.WaitAsync(0, ct))
                throw new InvalidOperationException(BusyMessage);
        }

        try
        {
            var server = await vpnServerQueryService.GetById(vpnServerId, ct)
                ?? throw new InvalidOperationException("VPN server not found.");

            if (server.ServerType != VpnServerType.OpenVpn)
                throw new InvalidOperationException("OpenVPN process control is only available for OpenVPN servers.");

            if (string.IsNullOrWhiteSpace(server.ApiUrl))
                throw new InvalidOperationException("API URL is not set for the server.");

            logger.LogInformation(
                "OpenVPN process {Method} {Path} for VpnServerId={VpnServerId}, ApiUrl={ApiUrl}",
                method.Method, relativePath, vpnServerId, server.ApiUrl);

            using var client = httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(server.ApiUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                tokenService.GenerateToken("vpn-cert-issuer", "cert-create", "backend", AudienceOpenVpnManager));

            using var request = new HttpRequestMessage(method, relativePath);
            using var response = await client.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var detail = await MicroserviceApiResponseHelper.ReadErrorMessageAsync(response, ct);
                logger.LogWarning(
                    "OpenVPN process call failed. VpnServerId={VpnServerId}, Path={Path}, Status={StatusCode}, Detail={Detail}",
                    vpnServerId, relativePath, (int)response.StatusCode, detail);

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail) ? BusyMessage : detail);

                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(detail)
                        ? $"OpenVPN process call failed (HTTP {(int)response.StatusCode})."
                        : detail);
            }

            return await MicroserviceApiResponseHelper.ReadSuccessDataAsync<OpenVpnProcessStatusResponse>(response, ct);
        }
        finally
        {
            gate?.Release();
        }
    }
}
