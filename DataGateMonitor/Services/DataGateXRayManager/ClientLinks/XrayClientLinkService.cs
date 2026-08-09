using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.IssuedXrayClientLinkTable;
using DataGateMonitor.DataBase.Services.Query.IssuedXrayClientLinkTokenTable;
using DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.Users;
using DataGateMonitor.Services.Others.Notifications.OvpnFileApi;
using DataGateMonitor.Services.VpnAccess;
using DataGateMonitor.SharedModels.DataGateMonitor.OpenVpnFiles.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.OpenVpnFiles.Responses;
using DataGateMonitor.SharedModels.DataGateMonitor.OpenVpnFiles.Responses.Dto;
using DataGateMonitor.SharedModels.Enums;
using Mapster;
using System.Text.RegularExpressions;

namespace DataGateMonitor.Services.DataGateXRayManager.ClientLinks;

public sealed class XrayClientLinkService(
    IXrayClientLinkMicroserviceClient xrayClientLinkMicroserviceClient,
    ILogger<XrayClientLinkService> logger,
    IConfiguration configuration,
    IVpnServerOvpnFileConfigQueryService vpnServerOvpnFileConfigQueryService,
    IIssuedXrayClientLinkQueryService issuedXrayClientLinkQueryService,
    IIssuedXrayClientLinkTokenQueryService issuedXrayClientLinkTokenQueryService,
    IUserIdentityLinkQueryService userIdentityLinkQueryService,
    IVpnServerQuotaPlanAccessGuard vpnServerQuotaPlanAccessGuard,
    ICommandService<IssuedXrayClientLink, int> issuedXrayClientLinkCommandService,
    ICommandService<IssuedXrayClientLinkToken, int> issuedXrayClientLinkTokenCommandService,
    IVpnServerQueryService vpnServerQueryService,
    IOvpnFileNotificationService ovpnFileNotificationService) : IXrayClientLinkService
{
    private const int DefaultTokenExpireDays = 1;

    private int TokenExpireDays =>
        configuration.GetValue<int?>("OvpnFileToken:ExpireDays") ?? DefaultTokenExpireDays;

    public async Task<IssuedXrayClientLink> GetByToken(string token, CancellationToken ct, bool isRevoked = false)
    {
        var issuedToken = await issuedXrayClientLinkTokenQueryService.GetByToken(token, ct)
                          ?? throw new InvalidOperationException(
                              $"IssuedXrayClientLinkToken not found for token='{token}'.");

        var link = await issuedXrayClientLinkQueryService.GetByIdAndIsRevoked(issuedToken.IssuedXrayClientLinkId,
                       isRevoked, ct)
                   ?? throw new InvalidOperationException(
                       $"IssuedXrayClientLink not found for token='{token}', link id {issuedToken.IssuedXrayClientLinkId}.");

        await ovpnFileNotificationService.NotifyReadByToken(
            token,
            link.Id,
            link.VpnServerId,
            isRevoked,
            ct,
            VpnProfileNotificationStack.Xray);

        return link;
    }

    public async Task<List<IssuedXrayClientLink>> GetAllByExternalId(string externalId, CancellationToken ct)
    {
        var queryKeys = await ResolveVpnExternalIdQueryKeysAsync(externalId, ct);
        var list = await QueryLinksByExternalIdKeysAsync(
            key => issuedXrayClientLinkQueryService.GetAllByExternalId(key, ct),
            queryKeys,
            ct);
        await ovpnFileNotificationService.NotifyReadByExternalId(queryKeys[0], list.Count, ct,
            VpnProfileNotificationStack.Xray);
        return list;
    }

    public async Task<List<IssuedXrayClientLink>> GetAllByVpnServerId(int vpnServerId, CancellationToken ct)
    {
        await RequireXrayServerAsync(vpnServerId, ct);
        var list = await issuedXrayClientLinkQueryService.GetAllByVpnServerId(vpnServerId, ct);
        await ovpnFileNotificationService.NotifyReadAll(vpnServerId, list.Count, ct, VpnProfileNotificationStack.Xray);
        return list;
    }

    public async Task<List<(IssuedXrayClientLink File, IssuedXrayClientLinkToken? Token)>> GetAllByVpnServerIdWithToken(
        int vpnServerId, CancellationToken ct, bool isRevoked = false)
    {
        await RequireXrayServerAsync(vpnServerId, ct);
        var files = await issuedXrayClientLinkQueryService.GetAllByVpnServerIdAndIsRevoked(vpnServerId, isRevoked, ct);
        var tokens = await issuedXrayClientLinkTokenQueryService.GetByIssuedLinkIds(files.Select(f => f.Id), ct);
        var result = files.Select(f =>
        {
            var t = tokens.FirstOrDefault(x => x.IssuedXrayClientLinkId == f.Id);
            return (File: f, Token: t);
        }).ToList();
        await ovpnFileNotificationService.NotifyReadAllWithToken(vpnServerId, result.Count, isRevoked, ct,
            VpnProfileNotificationStack.Xray);
        return result;
    }

    public async Task<List<IssuedXrayClientLink>> GetAllByExternalIdAndVpnServerId(int vpnServerId, string externalId,
        CancellationToken ct, bool isRevoked = false)
    {
        await RequireXrayServerAsync(vpnServerId, ct);
        var queryKeys = await ResolveVpnExternalIdQueryKeysAsync(externalId, ct);
        var result = await QueryLinksByExternalIdKeysAsync(
            key => issuedXrayClientLinkQueryService.GetAllByVpnServerIdAndExternalIdAndIsRevoked(
                vpnServerId, key, isRevoked, ct),
            queryKeys,
            ct);
        await ovpnFileNotificationService.NotifyReadByExternalIdAndVpnServerId(vpnServerId, queryKeys[0], result.Count,
            isRevoked, ct, VpnProfileNotificationStack.Xray);
        return result;
    }

    public async Task<List<(IssuedXrayClientLink File, IssuedXrayClientLinkToken? Token)>>
        GetAllByExternalIdAndVpnServerIdWithToken(int vpnServerId, string externalId, CancellationToken ct,
            bool isRevoked = false)
    {
        await RequireXrayServerAsync(vpnServerId, ct);
        var queryKeys = await ResolveVpnExternalIdQueryKeysAsync(externalId, ct);
        var files = await QueryLinksByExternalIdKeysAsync(
            key => issuedXrayClientLinkQueryService.GetAllByVpnServerIdAndExternalIdAndIsRevoked(
                vpnServerId, key, isRevoked, ct),
            queryKeys,
            ct);
        var tokens = await issuedXrayClientLinkTokenQueryService.GetByIssuedLinkIds(files.Select(f => f.Id), ct);
        var result = files.Select(f =>
        {
            var t = tokens.FirstOrDefault(x => x.IssuedXrayClientLinkId == f.Id);
            return (File: f, Token: t);
        }).ToList();
        await ovpnFileNotificationService.NotifyReadByExternalIdWithToken(vpnServerId, queryKeys[0], result.Count,
            isRevoked, ct, VpnProfileNotificationStack.Xray);
        return result;
    }

    public async Task<(IssuedXrayClientLink File, IssuedXrayClientLinkToken Token)> AddClientLinkWithToken(
        AddFileRequest request, CancellationToken ct)
    {
        var link = await AddClientLink(request, ct);
        var token = await MakeTokenForLink(link, ct);
        await ovpnFileNotificationService.NotifyIssuedWithToken(
            link.VpnServerId, link.Id, link.FileName, link.ExternalId, token.Id, ct, VpnProfileNotificationStack.Xray);
        return (link, token);
    }

    public async Task<IssuedXrayClientLink> AddClientLink(AddFileRequest request, CancellationToken ct)
    {
        logger.LogInformation("Add Xray client link: CommonName={Cn}, VpnServerId={Id}", request.CommonName,
            request.VpnServerId);

        request.ExternalId = await ResolveVpnExternalIdAsync(request.ExternalId, ct);
        await RequireTargetUserServerAccessAsync(request, ct);

        if (await issuedXrayClientLinkQueryService.ExistsActiveByVpnServerIdAndCommonName(
                request.VpnServerId, request.CommonName, ct))
        {
            throw new InvalidOperationException(
                $"Active client link with the same CommonName already exists. CommonName='{request.CommonName}', VpnServerId={request.VpnServerId}.");
        }

        var exportConfig = await GetExportConfigAsync(request.VpnServerId, ct);
        var friendlyName = await MakeFriendlyName(request.VpnServerId, request.CommonName, ct);
        var (serverIp, serverPort) = NormalizeServerEndpoint(exportConfig.VpnServerIp, exportConfig.VpnServerPort);
        if (serverIp != exportConfig.VpnServerIp || serverPort != exportConfig.VpnServerPort)
            logger.LogWarning(
                "VpnServerOvpnFileConfig endpoint normalized (host had :port while VpnServerPort was also set). VpnServerId={Id}, before {BeforeIp} port {BeforePort}, after {AfterIp} port {AfterPort}.",
                request.VpnServerId, exportConfig.VpnServerIp, exportConfig.VpnServerPort, serverIp, serverPort);

        var microRequest = new GenerateClientLinkMicroserviceRequest
        {
            CommonName = request.CommonName,
            FriendlyName = friendlyName,
            ConfigTemplate = exportConfig.ConfigTemplate,
            ServerIp = serverIp,
            ServerPort = serverPort,
            IssuedTo = request.IssuedTo,
            LinkExpireDays = request.OvpnFileExpireDays
        };

        var meta = await xrayClientLinkMicroserviceClient.AddClientLink(request.VpnServerId, microRequest, ct);
        var entity = ToNewEntity(request, meta);

        var saved = await issuedXrayClientLinkCommandService.Add(entity, true, ct);
        if (saved.Id <= 0)
            throw new InvalidOperationException(
                $"Client link was not persisted. CommonName='{request.CommonName}', VpnServerId={request.VpnServerId}.");

        await ovpnFileNotificationService.NotifyIssued(
            saved.VpnServerId, saved.Id, saved.FileName, saved.ExternalId, ct, VpnProfileNotificationStack.Xray);

        return await issuedXrayClientLinkQueryService.GetByVpnServerIdAndCommonName(
                   saved.Id, request.VpnServerId, request.CommonName, ct)
               ?? throw new InvalidOperationException("Client link was added but could not be reloaded.");
    }

    public async Task<IssuedXrayClientLink> RevokeClientLink(RevokeFileRequest request, CancellationToken ct)
    {
        await RequireXrayServerAsync(request.VpnServerId, ct);

        var link = await issuedXrayClientLinkQueryService.GetByIdAndVpnServerIdAndCommonNameAndIsRevoked(
            request.VpnServerId, request.OvpnFileId, request.CommonName, request.IsRevoked, ct);

        if (link is null)
        {
            throw new InvalidOperationException(
                $"Issued Xray client link not found for revocation. LinkId={request.OvpnFileId}, VpnServerId={request.VpnServerId}, CommonName={request.CommonName}, IsRevokedFilter={request.IsRevoked}.");
        }

        var revokeRequest = new RevokeClientLinkMicroserviceRequest
        {
            CommonName = link.CommonName,
            FileName = link.FileName,
            FilePath = link.FilePath
        };

        var meta = await xrayClientLinkMicroserviceClient.RevokeClientLink(request.VpnServerId, revokeRequest, ct);

        link.IsRevoked = true;
        link.FilePath = meta.FilePath;
        link.FileName = meta.FileName;
        if (!string.IsNullOrEmpty(meta.CertFilePath))
            link.CertFilePath = meta.CertFilePath;
        if (!string.IsNullOrEmpty(meta.KeyFilePath))
            link.KeyFilePath = meta.KeyFilePath;

        await issuedXrayClientLinkCommandService.Update(link, true, ct);

        await ovpnFileNotificationService.NotifyRevoked(
            link.VpnServerId, link.Id, link.FileName, link.ExternalId, ct, VpnProfileNotificationStack.Xray);

        return await issuedXrayClientLinkQueryService.GetByVpnServerIdAndCommonName(
                   link.Id, request.VpnServerId, request.CommonName, ct)
               ?? throw new InvalidOperationException("Client link was revoked but could not be reloaded.");
    }

    public async Task<DownloadFileResponse> DownloadClientLink(DownloadFileRequest request, CancellationToken ct,
        bool isRevoked = false)
    {
        await RequireXrayServerAsync(request.VpnServerId, ct);

        var link = await issuedXrayClientLinkQueryService.GetByIdAndVpnServerIdAndIsRevoked(
                       request.IssuedOvpnFileId, request.VpnServerId, isRevoked, ct)
                   ?? throw new InvalidOperationException(
                       $"Issued Xray client link not found. LinkId={request.IssuedOvpnFileId}, VpnServerId={request.VpnServerId}, IsRevoked={isRevoked}.");

        var downloadRequest = new DownloadClientLinkMicroserviceRequest
        {
            CommonName = link.CommonName,
            FileName = link.FileName,
            FilePath = link.FilePath
        };

        var result = await xrayClientLinkMicroserviceClient.DownloadClientLink(request.VpnServerId, downloadRequest, ct);

        await ovpnFileNotificationService.NotifyDownloaded(
            link.VpnServerId, link.FileName, link.ExternalId, isRevoked, ct, VpnProfileNotificationStack.Xray);

        return new DownloadFileResponse
        {
            IssuedOvpn = link.Adapt<IssuedOvpnFileDto>(),
            FileSizeBytes = result.Content.LongLength,
            Content = result.Content
        };
    }

    public async Task<DownloadFileResponse> DownloadClientLinkByCn(DownloadFileByCnRequest request, CancellationToken ct,
        bool isRevoked = false)
    {
        await RequireXrayServerAsync(request.VpnServerId, ct);

        var link = await issuedXrayClientLinkQueryService.GetByCommonNameAndVpnServerIdAndIsRevoked(
                       request.CommonName, request.VpnServerId, isRevoked, ct)
                   ?? throw new InvalidOperationException(
                       $"Issued Xray client link not found. CommonName={request.CommonName}, VpnServerId={request.VpnServerId}, IsRevoked={isRevoked}.");

        var downloadRequest = new DownloadClientLinkMicroserviceRequest
        {
            CommonName = link.CommonName,
            FileName = link.FileName,
            FilePath = link.FilePath
        };

        var result = await xrayClientLinkMicroserviceClient.DownloadClientLink(request.VpnServerId, downloadRequest, ct);

        await ovpnFileNotificationService.NotifyDownloaded(
            link.VpnServerId, link.FileName, link.ExternalId, isRevoked, ct, VpnProfileNotificationStack.Xray);

        return new DownloadFileResponse
        {
            IssuedOvpn = link.Adapt<IssuedOvpnFileDto>(),
            FileSizeBytes = result.Content.LongLength,
            Content = result.Content
        };
    }

    private Task<string> ResolveVpnExternalIdAsync(string externalId, CancellationToken ct) =>
        UserIdentityLinkExternalIdResolver.ResolveVpnExternalIdAsync(
            externalId,
            userIdentityLinkQueryService,
            ct);

    private Task RequireTargetUserServerAccessAsync(AddFileRequest request, CancellationToken ct) =>
        vpnServerQuotaPlanAccessGuard.EnsureTargetUserMayUseServerAsync(
            request.ExternalId,
            request.VpnServerId,
            ct);

    private Task<IReadOnlyList<string>> ResolveVpnExternalIdQueryKeysAsync(string externalId, CancellationToken ct) =>
        UserIdentityLinkExternalIdResolver.ResolveVpnExternalIdQueryKeysAsync(
            externalId,
            userIdentityLinkQueryService,
            ct);

    private static async Task<List<IssuedXrayClientLink>> QueryLinksByExternalIdKeysAsync(
        Func<string, Task<List<IssuedXrayClientLink>>> queryByKey,
        IReadOnlyList<string> queryKeys,
        CancellationToken ct)
    {
        if (queryKeys.Count == 1)
            return await queryByKey(queryKeys[0]);

        var seen = new HashSet<int>();
        var result = new List<IssuedXrayClientLink>();
        foreach (var key in queryKeys)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var link in await queryByKey(key))
            {
                if (seen.Add(link.Id))
                    result.Add(link);
            }
        }

        return result;
    }

    private async Task<IssuedXrayClientLinkToken> MakeTokenForLink(IssuedXrayClientLink link, CancellationToken ct)
    {
        var token = new IssuedXrayClientLinkToken
        {
            IssuedXrayClientLinkId = link.Id,
            Token = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(TokenExpireDays),
            IsUsed = false,
            Purpose = "download"
        };

        await issuedXrayClientLinkTokenCommandService.Add(token, true, ct);
        return token;
    }

    private async Task<VpnServerOvpnFileConfig> GetExportConfigAsync(int vpnServerId, CancellationToken ct)
    {
        await RequireXrayServerAsync(vpnServerId, ct);
        return await vpnServerOvpnFileConfigQueryService.GetByVpnServerIdId(vpnServerId, ct)
               ?? throw new InvalidOperationException(
                   "VPN server export configuration not found (template / endpoint for client links).");
    }

    private async Task RequireXrayServerAsync(int vpnServerId, CancellationToken ct)
    {
        var server = await vpnServerQueryService.GetById(vpnServerId, ct)
                     ?? throw new InvalidOperationException($"VPN server {vpnServerId} not found.");
        if (server.ServerType != VpnServerType.Xray)
            throw new InvalidOperationException(
                $"This operation is only supported for Xray servers. Server '{server.ServerName}' (id {vpnServerId}) has type {server.ServerType}.");
    }

    private async Task<string> MakeFriendlyName(int vpnServerId, string commonName, CancellationToken ct)
    {
        var vpnServer = await vpnServerQueryService.GetById(vpnServerId, ct)
                        ?? throw new InvalidOperationException("VPN server not found");

        var lastNumber = ExtractLastNumber(commonName);
        return lastNumber is not null ? $"{vpnServer.ServerName} [{lastNumber}]" : vpnServer.ServerName;
    }

    private static string? ExtractLastNumber(string input)
    {
        var match = Regex.Match(input, @"(\d+)$");
        return match.Success ? match.Value : null;
    }

    /// <summary>
    /// If <paramref name="vpnServerIp"/> is <c>hostname:30443</c> while <paramref name="vpnServerPort"/> is still
    /// <c>443</c>, the Xray manager would build <c>vless://…@hostname:30443:443</c>. Strip inline port when unambiguous.
    /// </summary>
    private static (string Host, int Port) NormalizeServerEndpoint(string vpnServerIp, int vpnServerPort)
    {
        vpnServerIp = (vpnServerIp ?? "").Trim();
        if (vpnServerIp.Length == 0)
            return (vpnServerIp, vpnServerPort);

        if (vpnServerIp[0] == '[')
        {
            var end = vpnServerIp.IndexOf(']', 1);
            if (end > 1 && end < vpnServerIp.Length - 2 && vpnServerIp[end + 1] == ':'
                && int.TryParse(vpnServerIp.AsSpan(end + 2), out var p6) && p6 is > 0 and <= 65535)
                return (vpnServerIp[..(end + 1)], p6);
            return (vpnServerIp, vpnServerPort);
        }

        if (vpnServerIp.Count(c => c == ':') == 1)
        {
            var idx = vpnServerIp.IndexOf(':');
            if (idx > 0 && int.TryParse(vpnServerIp.AsSpan(idx + 1), out var p) && p is > 0 and <= 65535)
                return (vpnServerIp[..idx], p);
        }

        return (vpnServerIp, vpnServerPort);
    }

    private static IssuedXrayClientLink ToNewEntity(AddFileRequest request, ClientLinkMetadataDto meta)
    {
        var issuedAt = meta.IssuedAt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(meta.IssuedAt, DateTimeKind.Utc)
            : meta.IssuedAt.ToUniversalTime();

        return new IssuedXrayClientLink
        {
            VpnServerId = request.VpnServerId,
            ExternalId = request.ExternalId,
            CommonName = meta.CommonName,
            CertId = "unavailable",
            FileName = meta.FileName,
            FilePath = meta.FilePath,
            IssuedAt = new DateTimeOffset(issuedAt),
            IssuedTo = meta.IssuedTo,
            PemFilePath = "unavailable",
            CertFilePath = meta.CertFilePath ?? "unavailable",
            KeyFilePath = meta.KeyFilePath ?? "unavailable",
            ReqFilePath = "unavailable",
            IsRevoked = false,
            Message = string.Empty
        };
    }
}
