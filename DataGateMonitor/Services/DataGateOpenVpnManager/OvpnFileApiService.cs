using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.IssuedOvpnFileTable;
using DataGateMonitor.DataBase.Services.Query.IssuedOvpnFileTokenTable;
using DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.Users;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.Services.Others.Notifications.OvpnFileApi;
using DataGateMonitor.Services.VpnAccess;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.OvpnFile.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.OpenVpnFiles.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.OpenVpnFiles.Responses;
using DataGateMonitor.SharedModels.DataGateMonitor.OpenVpnFiles.Responses.Dto;
using DataGateMonitor.SharedModels.Enums;
using Mapster;
using System.Text.RegularExpressions;

namespace DataGateMonitor.Services.DataGateOpenVpnManager;

/// <inheritdoc cref="IOvpnFileApiService" />
public class OvpnFileApiService(
    IOvpnFileApiClient ovpnFileApiClient,
    ILogger<OvpnFileApiService> logger,
    IConfiguration configuration,
    IVpnServerOvpnFileConfigQueryService openVpnServerOvpnFileConfigQueryService,
    IIssuedOvpnFileQueryService issuedOvpnFileQueryService,
    IIssuedOvpnFileTokenQueryService issuedOvpnFileTokenQueryService,
    IUserIdentityLinkQueryService userIdentityLinkQueryService,
    IVpnServerQuotaPlanAccessGuard vpnServerQuotaPlanAccessGuard,
    ICommandService<IssuedOvpnFile, int> issuedOvpnFileCommandService,
    ICommandService<IssuedOvpnFileToken, int> issuedOvpnFileTokenCommandService,
    IVpnServerQueryService openVpnServerQueryService,
    IOvpnFileNotificationService ovpnFileNotificationService) : IOvpnFileApiService
{
    private const int DefaultTokenExpireDays = 1;
    private const int OvpnDownloadMaxAttempts = 5;
    private static readonly TimeSpan OvpnDownloadRetryDelay = TimeSpan.FromMilliseconds(1000);

    private int TokenExpireDays =>
        configuration.GetValue<int?>("OvpnFileToken:ExpireDays") ?? DefaultTokenExpireDays;

    public async Task<IssuedOvpnFile> GetByToken(string token, CancellationToken ct, bool isRevoked = false)
    {
        var issuedOvpnFileToken = await issuedOvpnFileTokenQueryService
            .GetByToken(token, ct)
            ?? throw new InvalidOperationException(
                $"IssuedOvpnFileToken not found for token='{token}'. " +
                "Possible reasons: the token is invalid, expired, or already used/revoked.");

        var issuedOvpnFile = await issuedOvpnFileQueryService
                .GetByIdAndIsRevoked(issuedOvpnFileToken.IssuedOvpnFileId, isRevoked, ct)
            ?? throw new InvalidOperationException(
                $"IssuedOvpnFile not found for token='{token}'. " +
                $"IssuedOvpnFileId={issuedOvpnFileToken.IssuedOvpnFileId}, IsRevokedFilter={isRevoked}. " +
                "Possible reasons: the file was deleted, or its revoked state does not match the requested filter.");

        await ovpnFileNotificationService.NotifyReadByToken(
            token,
            issuedOvpnFile.Id,
            issuedOvpnFile.VpnServerId,
            isRevoked,
            ct);

        return issuedOvpnFile;
    }
    
    public async Task<List<IssuedOvpnFile>> GetAllByExternalId(string externalId, CancellationToken ct)
    {
        var queryKeys = await ResolveVpnExternalIdQueryKeysAsync(externalId, ct);
        var issuedFiles = await QueryIssuedFilesByExternalIdKeysAsync(
            key => issuedOvpnFileQueryService.GetAllByExternalId(key, ct),
            queryKeys,
            ct);
        
        await ovpnFileNotificationService.NotifyReadByExternalId(queryKeys[0], issuedFiles.Count, ct);

        return issuedFiles;
    }
    
    public async Task<List<IssuedOvpnFile>> GetAllByVpnServerId(int vpnServerId, CancellationToken ct)
    {
        var issuedFiles = await issuedOvpnFileQueryService.GetAllByVpnServerId(vpnServerId, ct);
        
        await ovpnFileNotificationService.NotifyReadAll(vpnServerId, issuedFiles.Count, ct);

        return issuedFiles;
    }

    public async Task<List<(IssuedOvpnFile File, IssuedOvpnFileToken? Token)>> GetAllByVpnServerIdWithToken(
        int vpnServerId, CancellationToken ct, bool isRevoked = false)
    {
        var issuedFiles = await issuedOvpnFileQueryService.GetAllByVpnServerIdAndIsRevoked(
            vpnServerId, isRevoked, ct);
        
        var tokens = await issuedOvpnFileTokenQueryService.GetByIssuedFileIds(
            issuedFiles.Select(x => x.Id).ToList(), ct);

        var result = issuedFiles.Select(file =>
        {
            var token = tokens.FirstOrDefault(t => t.IssuedOvpnFileId == file.Id);
            return (File: file, Token: token);
        }).ToList();

        
        await ovpnFileNotificationService.NotifyReadAllWithToken(vpnServerId, result.Count, isRevoked, ct);
        
        return result;
    }

    public async Task<List<IssuedOvpnFile>> GetAllByExternalIdAndVpnServerId(int vpnServerId, string externalId,
        CancellationToken ct, bool isRevoked = false)
    {
        var queryKeys = await ResolveVpnExternalIdQueryKeysAsync(externalId, ct);
        var result = await QueryIssuedFilesByExternalIdKeysAsync(
            key => issuedOvpnFileQueryService.GetAllByVpnServerIdAndExternalIdAndIsRevoked(
                vpnServerId, key, isRevoked, ct),
            queryKeys,
            ct);
        await ovpnFileNotificationService.NotifyReadByExternalIdAndVpnServerId(vpnServerId, queryKeys[0], result.Count,
            isRevoked, ct);

        return result;
    }

    public async Task<List<(IssuedOvpnFile File, IssuedOvpnFileToken? Token)>> 
        GetAllByExternalIdAndVpnServerIdWithToken(int vpnServerId, string externalId, 
            CancellationToken ct, bool isRevoked = false)
    {
        var queryKeys = await ResolveVpnExternalIdQueryKeysAsync(externalId, ct);
        var issuedFiles = await QueryIssuedFilesByExternalIdKeysAsync(
            key => issuedOvpnFileQueryService.GetAllByVpnServerIdAndExternalIdAndIsRevoked(
                vpnServerId, key, isRevoked, ct),
            queryKeys,
            ct);

        var tokens = await issuedOvpnFileTokenQueryService.GetByIssuedFileIds(
            issuedFiles.Select(x => x.Id).ToList(), ct);

        var result = issuedFiles
            .Select(file =>
            {
                var token = tokens.FirstOrDefault(t => t.IssuedOvpnFileId == file.Id);
                return (File: file, Token: token);
            })
            .ToList();
        
        await ovpnFileNotificationService.NotifyReadByExternalIdWithToken(vpnServerId, queryKeys[0], result.Count, 
            isRevoked, ct);

        return result;
    }

    public async Task<(IssuedOvpnFile File, IssuedOvpnFileToken Token)> AddOvpnFileWithToken(
        AddFileRequest request,
        CancellationToken ct)
    {
        var issuedOvpnFile = await AddOvpnFile(request, ct);
        var token = await MakeTokenForFile(issuedOvpnFile, ct);

        await ovpnFileNotificationService.NotifyIssuedWithToken(
            issuedOvpnFile.VpnServerId, issuedOvpnFile.Id, issuedOvpnFile.FileName,
            issuedOvpnFile.ExternalId, token.Id, /* todo: user ID*/ ct);
        
        return (issuedOvpnFile, token);
    }
    
    public async Task<IssuedOvpnFile> AddOvpnFile(
        AddFileRequest request, 
        CancellationToken ct)
    {
        logger.LogInformation(
            "Attempting to add new OVPN file: CommonName={CommonName}, VpnServerId={VpnServerId}",
            request.CommonName,
            request.VpnServerId);

        request.ExternalId = await ResolveVpnExternalIdAsync(request.ExternalId, ct);
        await RequireTargetUserServerAccessAsync(request, ct);
        
        if (await issuedOvpnFileQueryService
                .ExistsActiveByVpnServerIdAndCommonName(request.VpnServerId, request.CommonName, ct))
        {
            logger.LogWarning(
                "OVPN file already exists: CommonName={CommonName}, VpnServerId={VpnServerId}",
                request.CommonName,
                request.VpnServerId);

            throw new InvalidOperationException(
                $"Active OVPN file with the same CommonName already exists. " +
                $"CommonName='{request.CommonName}', VpnServerId={request.VpnServerId}.");
        }

        var ovpnFileConfig = await GetVpnServerOvpnFileConfig(request.VpnServerId, ct);

        var generateOvpnFileRequest = request.Adapt<GenerateOvpnFileRequest>();
        generateOvpnFileRequest.FriendlyΝame = await MakeFriendlyName(
            request.VpnServerId,
            request.CommonName,
            ct);
        generateOvpnFileRequest.ConfigTemplate = ovpnFileConfig.ConfigTemplate;
        generateOvpnFileRequest.ServerIp = ovpnFileConfig.VpnServerIp;
        generateOvpnFileRequest.ServerPort = ovpnFileConfig.VpnServerPort;
        
        var result = await ovpnFileApiClient.AddOvpnFile(
            request.VpnServerId,
            generateOvpnFileRequest,
            ct);

        var issuedOvpnFile = (request, result).Adapt<IssuedOvpnFile>();
        issuedOvpnFile.CertId = "unavailable";
        issuedOvpnFile.PemFilePath = "unavailable";
        issuedOvpnFile.ReqFilePath = "unavailable";
        issuedOvpnFile.IsRevoked = false;

        var newIssuedOvpnFile = await issuedOvpnFileCommandService.Add(
            issuedOvpnFile,
            true,
            ct);

        if (newIssuedOvpnFile.Id <= 0)
        {
            throw new InvalidOperationException(
                $"Issued OVPN file was not persisted. " +
                $"CommonName='{request.CommonName}', VpnServerId={request.VpnServerId}.");
        }

        logger.LogInformation(
            "OVPN file added successfully: CommonName={CommonName}, VpnServerId={VpnServerId}",
            request.CommonName,
            request.VpnServerId);
        
        await ovpnFileNotificationService.NotifyIssued(
            issuedOvpnFile.VpnServerId,
            issuedOvpnFile.Id,
            issuedOvpnFile.FileName,
            issuedOvpnFile.ExternalId,
            /* todo: user ID*/ ct);

        return await issuedOvpnFileQueryService
                   .GetByVpnServerIdAndCommonName(
                       newIssuedOvpnFile.Id,
                       request.VpnServerId,
                       request.CommonName,
                       ct)
               ?? throw new InvalidOperationException(
                   $"Issued OVPN file was added but could not be reloaded. " +
                   $"OvpnFileId={newIssuedOvpnFile.Id}, " +
                   $"VpnServerId={request.VpnServerId}, " +
                   $"CommonName={request.CommonName}. " +
                   "Possible reasons: the entity was deleted, or the query filters " +
                   "(server id / common name) do not match the stored record.");
    }

    public async Task<IssuedOvpnFile> RevokeOvpnFile(
        RevokeFileRequest request,
        CancellationToken ct)
    {
        await RequireOpenVpnServerAsync(request.VpnServerId, ct);

        logger.LogInformation(
            "Attempting to revoke OVPN file: OvpnFileId={OvpnFileId}, CommonName={CommonName}, " +
            "VpnServerId={VpnServerId}, IsRevokedFilter={IsRevokedFilter}",
            request.OvpnFileId,
            request.CommonName,
            request.VpnServerId,
            request.IsRevoked);

        var issuedOvpnFile = await issuedOvpnFileQueryService
            .GetByIdAndVpnServerIdAndCommonNameAndIsRevoked(
                request.VpnServerId,
                request.OvpnFileId,
                request.CommonName,
                request.IsRevoked,
                ct);
        
        if (issuedOvpnFile == null)
        {
            logger.LogWarning(
                "Issued OVPN file not found for revocation: OvpnFileId={OvpnFileId}, CommonName={CommonName}, " +
                "VpnServerId={VpnServerId}, IsRevokedFilter={IsRevokedFilter}",
                request.OvpnFileId,
                request.CommonName,
                request.VpnServerId,
                request.IsRevoked);

            throw new InvalidOperationException(
                $"Issued OVPN file not found for revocation. " +
                $"OvpnFileId={request.OvpnFileId}, " +
                $"VpnServerId={request.VpnServerId}, " +
                $"CommonName={request.CommonName}, " +
                $"IsRevokedFilter={request.IsRevoked}. " +
                "Possible reasons: the file does not exist, belongs to another VPN server, " +
                "has a different CommonName, or its revoked state does not match the requested filter.");
        }
        
        var revokeOvpnFileRequest = request.Adapt<RevokeOvpnFileRequest>();

        revokeOvpnFileRequest.OvpnFileName = issuedOvpnFile.FileName
            ?? throw new InvalidOperationException(
                $"Cannot revoke OVPN file because FileName is null in DB. " +
                $"OvpnFileId={issuedOvpnFile.Id}, " +
                $"VpnServerId={issuedOvpnFile.VpnServerId}, " +
                $"CommonName={issuedOvpnFile.CommonName}.");

        revokeOvpnFileRequest.OvpnFilePath = issuedOvpnFile.FilePath;
            
        var result = await ovpnFileApiClient.RevokeOvpnFile(
            request.VpnServerId,
            revokeOvpnFileRequest,
            ct);

        issuedOvpnFile.IsRevoked = result;
        
        await issuedOvpnFileCommandService.Update(
            issuedOvpnFile,
            true,
            ct);

        logger.LogInformation(
            "OVPN file revoked: OvpnFileId={OvpnFileId}, CommonName={CommonName}, VpnServerId={VpnServerId}, " +
            "ExternalId={ExternalId}, IsRevoked={IsRevoked}",
            issuedOvpnFile.Id,
            issuedOvpnFile.CommonName,
            issuedOvpnFile.VpnServerId,
            issuedOvpnFile.ExternalId,
            issuedOvpnFile.IsRevoked);
        
        await ovpnFileNotificationService.NotifyRevoked(
            issuedOvpnFile.VpnServerId,
            issuedOvpnFile.Id,
            issuedOvpnFile.FileName,
            issuedOvpnFile.ExternalId,
            /* todo: user ID*/ ct);

        return await issuedOvpnFileQueryService
                   .GetByVpnServerIdAndCommonName(
                       issuedOvpnFile.Id,
                       request.VpnServerId,
                       request.CommonName,
                       ct)
               ?? throw new InvalidOperationException(
                   $"Issued OVPN file was revoked but could not be reloaded from DB. " +
                   $"OvpnFileId={issuedOvpnFile.Id}, " +
                   $"VpnServerId={request.VpnServerId}, " +
                   $"CommonName={request.CommonName}. " +
                   "Check that the entity was not deleted and that the query filters " +
                   "(server id / common name / revoked state) are correct.");
    }

    public async Task<DownloadFileResponse> DownloadOvpnFile(DownloadFileRequest request,
        CancellationToken ct, bool isRevoked = false)
    {
        await RequireOpenVpnServerAsync(request.VpnServerId, ct);

        logger.LogInformation("Start downloading OVPN file:" +
                              " VpnServerId={VpnServerId}, IssuedOvpnFileId={IssuedOvpnFileId}",
            request.VpnServerId, request.IssuedOvpnFileId);

        return await ExecuteDownloadWithRetriesAsync(
            request.VpnServerId,
            async innerCt =>
            {
                var issuedOvpnFile = await issuedOvpnFileQueryService
                        .GetByIdAndVpnServerIdAndIsRevoked(
                            request.IssuedOvpnFileId,
                            request.VpnServerId,
                            isRevoked,
                            innerCt)
                    ?? throw new InvalidOperationException(
                        $"Issued OVPN file not found. " +
                        $"Requested IssuedOvpnFileId={request.IssuedOvpnFileId}, " +
                        $"VpnServerId={request.VpnServerId}, " +
                        $"IsRevoked={isRevoked}. " +
                        "Possible reasons: the file does not exist, belongs to another server, " +
                        "or its revoke state does not match the requested one.");

                return await DownloadIssuedFileFromMicroserviceAsync(issuedOvpnFile, isRevoked, innerCt);
            },
            ct);
    }

    private async Task<IssuedOvpnFileToken> MakeTokenForFile(IssuedOvpnFile issuedOvpnFile, CancellationToken ct)
    {
        var token = new IssuedOvpnFileToken
        {
            IssuedOvpnFileId = issuedOvpnFile.Id,
            Token = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(TokenExpireDays),
            IsUsed = false,
            Purpose = "download"
        };

        await issuedOvpnFileTokenCommandService.Add(token, true, ct);
        return token;
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

    private static async Task<List<IssuedOvpnFile>> QueryIssuedFilesByExternalIdKeysAsync(
        Func<string, Task<List<IssuedOvpnFile>>> queryByKey,
        IReadOnlyList<string> queryKeys,
        CancellationToken ct)
    {
        if (queryKeys.Count == 1)
            return await queryByKey(queryKeys[0]);

        var seen = new HashSet<int>();
        var result = new List<IssuedOvpnFile>();
        foreach (var key in queryKeys)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var file in await queryByKey(key))
            {
                if (seen.Add(file.Id))
                    result.Add(file);
            }
        }

        return result;
    }

    private async Task<VpnServerOvpnFileConfig> GetVpnServerOvpnFileConfig(int vpnServerId, 
        CancellationToken ct)
    {
        await RequireOpenVpnServerAsync(vpnServerId, ct);

        return await openVpnServerOvpnFileConfigQueryService.GetByVpnServerIdId(vpnServerId, ct) 
               ?? throw new InvalidOperationException("OpenVPN File Server Config not found");
    }

    private async Task RequireOpenVpnServerAsync(int vpnServerId, CancellationToken ct)
    {
        var server = await openVpnServerQueryService.GetById(vpnServerId, ct)
            ?? throw new InvalidOperationException($"VPN server {vpnServerId} not found.");
        if (server.ServerType != VpnServerType.OpenVpn)
            throw new InvalidOperationException(
                "This operation is only supported for OpenVPN servers. " +
                $"Server '{server.ServerName}' (id {vpnServerId}) has type {server.ServerType}.");
    }
    
    private async Task<string> MakeFriendlyName(int vpnServerId, string commonName, CancellationToken ct)
    {
        var vpnServer = await openVpnServerQueryService.GetById(vpnServerId, ct)
                        ?? throw new InvalidOperationException("OpenVPN Server not found");

        var lastNumber = ExtractLastNumber(commonName);

        return lastNumber is not null
            ? $"{vpnServer.ServerName} [{lastNumber}]"
            : vpnServer.ServerName;
    }
    
    private string? ExtractLastNumber(string input)
    {
        var match = Regex.Match(input, @"(\d+)$");
        return match.Success ? match.Value : null;
    }
    
    public async Task<DownloadFileResponse> DownloadOvpnFileByCn(DownloadFileByCnRequest request, CancellationToken ct,
        bool isRevoked = false)
    {
        await RequireOpenVpnServerAsync(request.VpnServerId, ct);

        logger.LogInformation("Start downloading OVPN file:" +
                              " VpnServerId={VpnServerId}, CommonName={CommonName}",
            request.VpnServerId, request.CommonName);

        return await ExecuteDownloadWithRetriesAsync(
            request.VpnServerId,
            async innerCt =>
            {
                var issuedOvpnFile = await issuedOvpnFileQueryService
                        .GetByCommonNameAndVpnServerIdAndIsRevoked(
                            request.CommonName,
                            request.VpnServerId,
                            isRevoked,
                            innerCt)
                    ?? throw new InvalidOperationException(
                        $"Issued OVPN file not found. " +
                        $"Requested CommonName={request.CommonName}, " +
                        $"VpnServerId={request.VpnServerId}, " +
                        $"IsRevoked={isRevoked}. " +
                        "Possible reasons: the file does not exist, belongs to another server, " +
                        "or its revoke state does not match the requested one.");

                return await DownloadIssuedFileFromMicroserviceAsync(issuedOvpnFile, isRevoked, innerCt);
            },
            ct);
    }

    private async Task<DownloadFileResponse> ExecuteDownloadWithRetriesAsync(
        int vpnServerId,
        Func<CancellationToken, Task<DownloadFileResponse>> downloadAttempt,
        CancellationToken ct)
    {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= OvpnDownloadMaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                return await downloadAttempt(ct);
            }
            catch (Exception ex) when (attempt < OvpnDownloadMaxAttempts && IsRetriableOvpnDownloadFailure(ex))
            {
                lastError = ex;
                logger.LogDebug(
                    ex,
                    "OVPN download attempt {Attempt}/{MaxAttempts} failed for VpnServerId={VpnServerId}, " +
                    "retrying in {DelayMs}ms",
                    attempt,
                    OvpnDownloadMaxAttempts,
                    vpnServerId,
                    OvpnDownloadRetryDelay.TotalMilliseconds);
                await Task.Delay(OvpnDownloadRetryDelay, ct);
            }
        }

        throw lastError ?? new InvalidOperationException("OVPN download failed after all retry attempts.");
    }

    private static bool IsRetriableOvpnDownloadFailure(Exception ex) =>
        ex is HttpRequestException or Newtonsoft.Json.JsonException
        || ex is InvalidOperationException;

    private async Task<DownloadFileResponse> DownloadIssuedFileFromMicroserviceAsync(
        IssuedOvpnFile issuedOvpnFile,
        bool isRevoked,
        CancellationToken ct)
    {
        var requestApi = new DownloadOvpnFileRequest
        {
            CommonName = issuedOvpnFile.CommonName,
            FileName = issuedOvpnFile.FileName,
            FilePath = issuedOvpnFile.FilePath
        };

        var result = await ovpnFileApiClient.DownloadOvpnFile(
            issuedOvpnFile.VpnServerId, requestApi, ct);

        if (result.Content == null)
        {
            logger.LogError(
                "Downloaded OVPN file content is null: FileName={FileName}, CommonName={CommonName}",
                result.FileName,
                result.CommonName);

            throw new InvalidOperationException(
                "Downloaded OVPN file content is null. " +
                $"FileName={result.FileName}, CommonName={result.CommonName}.");
        }

        logger.LogInformation("Successfully downloaded OVPN file: FileName={FileName}, Size={Size} bytes",
            result.FileName, result.Content.LongLength);

        await ovpnFileNotificationService.NotifyDownloaded(
            issuedOvpnFile.VpnServerId, issuedOvpnFile.FileName, issuedOvpnFile.ExternalId,
            isRevoked, /* todo: user ID*/ ct);

        return new DownloadFileResponse
        {
            IssuedOvpn = issuedOvpnFile.Adapt<IssuedOvpnFileDto>(),
            FileSizeBytes = result.Content.LongLength,
            Content = result.Content
        };
    }
}
