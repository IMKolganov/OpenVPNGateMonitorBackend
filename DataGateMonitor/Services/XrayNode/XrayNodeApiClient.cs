using System.Net.Http.Headers;
using DataGateMonitor.Models.XrayNode;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.Serialization;
using DataGateMonitor.SharedModels.DataGateXRayManager.XrayClients;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.Services.XrayNode;

public sealed class XrayNodeApiClient(
    ILogger<XrayNodeApiClient> logger,
    IHttpClientFactory httpClientFactory,
    IMicroserviceTokenService tokenService) : IXrayNodeApiClient
{
    public const string HttpClientName = "XrayNodeApi";

    internal const string ClientsRelativePath = "api/xray/clients";
    internal const string KickRelativePath = "api/xray/clients/kick";
    internal const string DisableRelativePath = "api/xray/users/disable";
    private const string AudienceXRayManager = "DataGateXRayManager";

    public async Task<XrayNodeClientsResponse?> GetActiveClientsAsync(string baseApiUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(baseApiUrl))
            throw new ArgumentException("Base API URL is required.", nameof(baseApiUrl));

        var baseUri = baseApiUrl.TrimEnd('/') + "/";
        var requestUri = new Uri(new Uri(baseUri, UriKind.Absolute), ClientsRelativePath);

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
            tokenService.GenerateToken("vpn-cert-issuer", "cert-create", "backend", AudienceXRayManager));

        using var response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Xray node clients request failed: {StatusCode} {Reason} for {Uri}",
                (int)response.StatusCode, response.ReasonPhrase, requestUri);
            return null;
        }

        var wrapped = await ProjectJson.ReadContentAsync<ApiResponse<XrayClientsEnvelope>>(response.Content, cancellationToken);
        if (wrapped is not { Success: true, Data: not null })
            return new XrayNodeClientsResponse();

        return MapEnvelopeToNodeResponse(wrapped.Data);
    }

    public async Task KickUserAsync(string baseApiUrl, string commonName, CancellationToken cancellationToken)
    {
        await PostJsonExpectSuccessAsync(baseApiUrl, KickRelativePath,
            new { commonName }, cancellationToken, "kick");
    }

    public async Task DisableUserAsync(string baseApiUrl, string commonName, CancellationToken cancellationToken)
    {
        await PostJsonExpectSuccessAsync(baseApiUrl, DisableRelativePath,
            new { commonName }, cancellationToken, "disable user");
    }

    private async Task PostJsonExpectSuccessAsync(string baseApiUrl, string relativePath, object body,
        CancellationToken cancellationToken, string actionLabel)
    {
        if (string.IsNullOrWhiteSpace(baseApiUrl))
            throw new ArgumentException("Base API URL is required.", nameof(baseApiUrl));

        var baseUri = baseApiUrl.TrimEnd('/') + "/";
        var requestUri = new Uri(new Uri(baseUri, UriKind.Absolute), relativePath);
        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = ProjectJson.ToJsonContent(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
            tokenService.GenerateToken("vpn-cert-issuer", "cert-create", "backend", AudienceXRayManager));

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await MicroserviceApiResponseHelper.ReadErrorMessageAsync(response, cancellationToken);
            logger.LogWarning("Xray node {Action} failed: {Status} {Detail}", actionLabel,
                (int)response.StatusCode, detail);
            throw new HttpRequestException(
                $"Xray node {actionLabel} failed ({(int)response.StatusCode}): {detail}", null, response.StatusCode);
        }
    }

    private static XrayNodeClientsResponse MapEnvelopeToNodeResponse(XrayClientsEnvelope envelope) =>
        new()
        {
            Clients = envelope.Clients.Select(c => new XrayNodeClientDto
            {
                Email = c.Email,
                RemoteAddress = c.RemoteAddress,
                ProxyRealIp = c.ProxyRealIp,
                Username = c.Username,
                BytesReceived = c.BytesReceived,
                BytesSent = c.BytesSent,
                ConnectedSince = c.ConnectedSince,
            }).ToList(),
            PollError = envelope.PollError,
            PolledAt = envelope.PolledAt,
        };
}
