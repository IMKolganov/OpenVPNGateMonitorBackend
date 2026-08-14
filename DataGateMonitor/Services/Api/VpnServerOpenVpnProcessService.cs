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

    public Task<OpenVpnProcessStatusResponse> GetStatusAsync(int vpnServerId, CancellationToken ct) =>
        SendAsync(vpnServerId, HttpMethod.Get, "api/openvpn/status", ct);

    public Task<OpenVpnProcessStatusResponse> StartAsync(int vpnServerId, CancellationToken ct) =>
        SendAsync(vpnServerId, HttpMethod.Post, "api/openvpn/start", ct);

    public Task<OpenVpnProcessStatusResponse> RestartAsync(int vpnServerId, CancellationToken ct) =>
        SendAsync(vpnServerId, HttpMethod.Post, "api/openvpn/restart", ct);

    public Task<OpenVpnProcessStatusResponse> KillAsync(int vpnServerId, CancellationToken ct) =>
        SendAsync(vpnServerId, HttpMethod.Post, "api/openvpn/kill", ct);

    private async Task<OpenVpnProcessStatusResponse> SendAsync(
        int vpnServerId,
        HttpMethod method,
        string relativePath,
        CancellationToken ct)
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
            throw new InvalidOperationException(
                $"OpenVPN process call failed. Status: {(int)response.StatusCode}. Details: {detail}");
        }

        return await MicroserviceApiResponseHelper.ReadSuccessDataAsync<OpenVpnProcessStatusResponse>(response, ct);
    }
}
