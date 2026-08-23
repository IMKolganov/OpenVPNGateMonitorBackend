using System.Net.Http.Headers;
using DataGateMonitor.DataBase.Services.Query;
using DataGateMonitor.DataBase.Services.Query.VpnDnsQueryLogTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerPiHoleConfigTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Models;
using DataGateMonitor.Serialization;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.Services.Api.Interfaces;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerPiHole.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerPiHole.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerPiHole.Responses;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.Diagnostics.Responses;
using DataGateMonitor.SharedModels.Enums;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DataGateMonitor.Services.Api;

public class VpnServerPiHoleConfigService(
    IVpnServerQueryService vpnServerQueryService,
    IVpnServerPiHoleConfigQueryService piHoleConfigQuery,
    IVpnDnsQueryLogQueryService dnsQueryLogQuery,
    IQueryService<VpnServerPiHoleConfig, int> piHoleQuery,
    ICommandService<VpnServerPiHoleConfig, int> piHoleConfigCommand,
    IHttpClientFactory httpClientFactory,
    IMicroserviceTokenService tokenService,
    ILogger<VpnServerPiHoleConfigService> logger) : IVpnServerPiHoleConfigService
{
    private const string MaskedPassword = "********";
    private const string AudienceOpenVpnManager = "DataGateOpenVpnManager";
    private const string AudienceXrayManager = "DataGateXRayManager";

    public async Task<VpnServerPiHoleConfigResponse> GetForAdminAsync(int vpnServerId, CancellationToken ct)
    {
        _ = await vpnServerQueryService.GetById(vpnServerId, ct)
            ?? throw new InvalidOperationException("VPN server not found.");

        var config = await piHoleConfigQuery.GetByVpnServerId(vpnServerId, ct);
        return new VpnServerPiHoleConfigResponse
        {
            Config = ToAdminDto(vpnServerId, config)
        };
    }

    public async Task<VpnServerPiHoleConfigResponse> UpsertAsync(UpsertVpnServerPiHoleConfigRequest request, CancellationToken ct)
    {
        _ = await vpnServerQueryService.GetById(request.VpnServerId, ct)
            ?? throw new InvalidOperationException("VPN server not found.");

        var existing = await piHoleQuery.Query(asNoTracking: false)
            .FirstOrDefaultAsync(x => x.VpnServerId == request.VpnServerId, ct);
        var now = DateTimeOffset.UtcNow;
        var isCreate = existing is null;

        if (existing is null)
        {
            var row = new VpnServerPiHoleConfig
            {
                VpnServerId = request.VpnServerId,
                BaseUrl = request.BaseUrl.Trim(),
                AppPassword = request.AppPassword?.Trim() ?? string.Empty,
                PollIntervalSeconds = request.PollIntervalSeconds,
                BatchSize = request.BatchSize,
                LookbackSeconds = request.LookbackSeconds,
                ClientSubnetPrefix = PiHoleClientSubnetPrefix.Normalize(request.ClientSubnetPrefix),
                CreateDate = now,
                LastUpdate = now
            };
            await piHoleConfigCommand.Add(row, saveChanges: true, ct);
        }
        else
        {
            existing.BaseUrl = request.BaseUrl.Trim();
            if (!string.IsNullOrWhiteSpace(request.AppPassword))
                existing.AppPassword = request.AppPassword.Trim();
            existing.PollIntervalSeconds = request.PollIntervalSeconds;
            existing.BatchSize = request.BatchSize;
            existing.LookbackSeconds = request.LookbackSeconds;
            existing.ClientSubnetPrefix = PiHoleClientSubnetPrefix.Normalize(request.ClientSubnetPrefix);
            existing.LastUpdate = now;
            await piHoleConfigCommand.SaveChanges(ct);
        }

        logger.LogInformation(
            "Pi-hole config {Action} for VpnServerId={VpnServerId}: BaseUrl={BaseUrl}, PollIntervalSec={PollIntervalSec}, BatchSize={BatchSize}, HasPassword={HasPassword}",
            isCreate ? "created" : "updated",
            request.VpnServerId,
            request.BaseUrl.Trim(),
            request.PollIntervalSeconds,
            request.BatchSize,
            !string.IsNullOrWhiteSpace(request.AppPassword) || (existing?.AppPassword?.Length ?? 0) > 0);

        var saved = await piHoleConfigQuery.GetByVpnServerId(request.VpnServerId, ct);
        return new VpnServerPiHoleConfigResponse
        {
            Config = ToAdminDto(request.VpnServerId, saved)
        };
    }

    public async Task<VpnServerPiHoleRuntimeConfigResponse?> GetRuntimeForMicroserviceAsync(int vpnServerId, CancellationToken ct)
    {
        var server = await vpnServerQueryService.GetById(vpnServerId, ct);
        if (server is null || !server.IsPiHoleEnabled)
            return null;

        var config = await piHoleConfigQuery.GetByVpnServerId(vpnServerId, ct);
        if (config is null)
            return null;

        return new VpnServerPiHoleRuntimeConfigResponse
        {
            VpnServerId = vpnServerId,
            IsPiHoleEnabled = true,
            BaseUrl = config.BaseUrl,
            AppPassword = config.AppPassword,
            PollIntervalSeconds = config.PollIntervalSeconds,
            BatchSize = config.BatchSize,
            LookbackSeconds = config.LookbackSeconds,
            ClientSubnetPrefix = config.ClientSubnetPrefix
        };
    }

    public async Task ApplyRuntimeToMicroserviceAsync(int vpnServerId, CancellationToken ct)
    {
        var server = await vpnServerQueryService.GetById(vpnServerId, ct)
            ?? throw new InvalidOperationException("VPN server not found.");

        if (string.IsNullOrWhiteSpace(server.ApiUrl))
            throw new InvalidOperationException("API URL is not set for the server.");

        var runtime = await GetRuntimeForMicroserviceAsync(vpnServerId, ct)
            ?? throw new InvalidOperationException("Pi-hole integration is disabled or not configured.");

        var audience = MicroserviceAudience(server.ServerType);
        logger.LogInformation(
            "Applying Pi-hole runtime to microservice. VpnServerId={VpnServerId}, ServerType={ServerType}, ApiUrl={ApiUrl}, BaseUrl={BaseUrl}",
            vpnServerId,
            server.ServerType,
            server.ApiUrl,
            runtime.BaseUrl);

        using var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(server.ApiUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            tokenService.GenerateToken("vpn-cert-issuer", "cert-create", "backend", audience));

        var body = new
        {
            enabled = true,
            baseUrl = runtime.BaseUrl,
            appPassword = runtime.AppPassword,
            pollIntervalSeconds = runtime.PollIntervalSeconds,
            batchSize = runtime.BatchSize,
            lookbackSeconds = runtime.LookbackSeconds,
            clientSubnetPrefix = runtime.ClientSubnetPrefix
        };

        using var response = await client.PutAsync(
            "api/pi-hole/config",
            ProjectJson.ToJsonContent(body),
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var detail = await MicroserviceApiResponseHelper.ReadErrorMessageAsync(response, ct);
            logger.LogWarning(
                "Pi-hole runtime apply failed. VpnServerId={VpnServerId}, Status={StatusCode}, Detail={Detail}",
                vpnServerId,
                (int)response.StatusCode,
                detail);
            throw new InvalidOperationException(
                $"Failed to apply Pi-hole config to microservice. Status: {(int)response.StatusCode}. Details: {detail}");
        }

        var appliedAt = DateTimeOffset.UtcNow;
        var row = await piHoleQuery.Query(asNoTracking: false)
            .FirstOrDefaultAsync(x => x.VpnServerId == vpnServerId, ct);
        if (row is not null)
        {
            row.LastRuntimeAppliedAtUtc = appliedAt;
            row.LastUpdate = appliedAt;
            await piHoleConfigCommand.SaveChanges(ct);
        }

        logger.LogInformation(
            "Pi-hole runtime applied successfully. VpnServerId={VpnServerId}, BaseUrl={BaseUrl}",
            vpnServerId,
            runtime.BaseUrl);
    }

    public async Task<PiHoleDiagnosticsResponse> GetMicroserviceDiagnosticsAsync(int vpnServerId, CancellationToken ct)
    {
        var server = await vpnServerQueryService.GetById(vpnServerId, ct)
            ?? throw new InvalidOperationException("VPN server not found.");

        if (string.IsNullOrWhiteSpace(server.ApiUrl))
            throw new InvalidOperationException("API URL is not set for the server.");

        var audience = MicroserviceAudience(server.ServerType);
        logger.LogDebug(
            "Fetching Pi-hole diagnostics from microservice. VpnServerId={VpnServerId}, ServerType={ServerType}, ApiUrl={ApiUrl}",
            vpnServerId,
            server.ServerType,
            server.ApiUrl);

        using var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(server.ApiUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            tokenService.GenerateToken("vpn-cert-issuer", "cert-create", "backend", audience));

        using var response = await client.GetAsync("api/pi-hole/diagnostics", ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await MicroserviceApiResponseHelper.ReadErrorMessageAsync(response, ct);
            logger.LogWarning(
                "Pi-hole diagnostics request failed. VpnServerId={VpnServerId}, Status={StatusCode}, Detail={Detail}",
                vpnServerId,
                (int)response.StatusCode,
                detail);
            throw new InvalidOperationException(
                $"Failed to read Pi-hole diagnostics from microservice. Status: {(int)response.StatusCode}. Details: {detail}");
        }

        var diagnostics = await MicroserviceApiResponseHelper.ReadSuccessDataAsync<PiHoleDiagnosticsResponse>(response, ct);
        var (storedCount, lastStoredAt) = await dnsQueryLogQuery.GetServerSummaryAsync(vpnServerId, ct);
        diagnostics.StoredQueryCount = storedCount;
        diagnostics.LastStoredQueryAtUtc = lastStoredAt?.UtcDateTime;
        PiHoleDiagnosticsHealth.Apply(diagnostics, server.IsPiHoleEnabled);

        logger.LogInformation(
            "Pi-hole diagnostics for VpnServerId={VpnServerId}: Health={Health}, Authenticated={Authenticated}, CollectorRunning={CollectorRunning}, LastPollError={LastPollError}, Stored={StoredCount}",
            vpnServerId,
            diagnostics.Health,
            diagnostics.Authenticated,
            diagnostics.CollectorRunning,
            diagnostics.LastPollError,
            diagnostics.StoredQueryCount);

        return diagnostics;
    }

    private static string MicroserviceAudience(VpnServerType serverType) =>
        serverType == VpnServerType.Xray ? AudienceXrayManager : AudienceOpenVpnManager;

    private static VpnServerPiHoleConfigDto ToAdminDto(int vpnServerId, VpnServerPiHoleConfig? config)
    {
        if (config is null)
        {
            return new VpnServerPiHoleConfigDto
            {
                VpnServerId = vpnServerId
            };
        }

        var hasPassword = !string.IsNullOrEmpty(config.AppPassword);
        return new VpnServerPiHoleConfigDto
        {
            VpnServerId = config.VpnServerId,
            BaseUrl = config.BaseUrl,
            AppPassword = hasPassword ? MaskedPassword : string.Empty,
            HasAppPassword = hasPassword,
            PollIntervalSeconds = config.PollIntervalSeconds,
            BatchSize = config.BatchSize,
            LookbackSeconds = config.LookbackSeconds,
            ClientSubnetPrefix = config.ClientSubnetPrefix
        };
    }
}
