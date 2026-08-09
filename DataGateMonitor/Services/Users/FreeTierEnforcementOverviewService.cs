using DataGateMonitor.DataBase.Services.Query.IssuedOvpnFileTable;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;
using DataGateMonitor.DataBase.Services.Query.UserQuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerClientTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Models;
using DataGateMonitor.Models.Helpers;
using DataGateMonitor.Services.Users.Interfaces;
using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Responses;
using DataGateMonitor.SharedModels.Enums;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.Services.Users;

public sealed class FreeTierEnforcementOverviewService(
    IUserQuotaPlanQueryService userQuotaPlanQueryService,
    IQuotaPlanQueryService quotaPlanQueryService,
    IUserQueryService userQueryService,
    IUserIdentityLinkQueryService userIdentityLinkQueryService,
    IIssuedOvpnFileQueryService issuedOvpnFileQueryService,
    IVpnServerClientQueryService vpnServerClientQueryService,
    IVpnServerQueryService vpnServerQueryService,
    IFreeTierAccessComplianceService freeTierAccessComplianceService,
    IQueryService<FreeTierDisconnectLog, int> disconnectLogQueryService,
    ILogger<FreeTierEnforcementOverviewService> logger) : IFreeTierEnforcementOverviewService
{
    public Task<GetFreeTierEnforcementCandidatesResponse> GetCandidatesAsync(CancellationToken ct = default)
        => CollectAsync(
            include: result => result.IsApplicable && !result.IsCompliant,
            connectedOnly: false,
            ct);

    public Task<GetFreeTierEnforcementCandidatesResponse> GetUnsubscribedConnectedAsync(
        CancellationToken ct = default)
        // Online digest: start from connected sessions so we do not call Telegram for every Free/Default user.
        => CollectConnectedUnsubscribedAsync(ct);

    private async Task<GetFreeTierEnforcementCandidatesResponse> CollectConnectedUnsubscribedAsync(
        CancellationToken ct)
    {
        var freeTierUserIds = await GetFreeTierUserIdsAsync(ct);
        if (freeTierUserIds.Count == 0)
            return new GetFreeTierEnforcementCandidatesResponse();

        var servers = await vpnServerQueryService.GetAll(ct: ct);
        var serverNameById = servers.ToDictionary(s => s.Id, s => s.ServerName);

        var connected = (await vpnServerClientQueryService.GetAllConnected(ct))
            .Where(c => !string.IsNullOrWhiteSpace(c.CommonName))
            .ToList();
        if (connected.Count == 0)
            return new GetFreeTierEnforcementCandidatesResponse();

        var userIdByConnection = await ResolveConnectedUserIdsAsync(connected, ct);
        var connectedByUserId = new Dictionary<int, VpnServerClient>();
        foreach (var client in connected)
        {
            if (!userIdByConnection.TryGetValue(client, out var userId))
                continue;
            if (!freeTierUserIds.Contains(userId))
                continue;
            connectedByUserId.TryAdd(userId, client);
        }

        if (connectedByUserId.Count == 0)
            return new GetFreeTierEnforcementCandidatesResponse();

        var userIds = connectedByUserId.Keys.ToList();
        var usersById = await userQueryService.GetByIds(userIds, ct);
        var linksByUserId = await userIdentityLinkQueryService.GetListByUserIds(userIds, ct);

        var candidates = new List<FreeTierEnforcementCandidateDto>(userIds.Count);
        foreach (var userId in userIds)
        {
            ct.ThrowIfCancellationRequested();

            FreeTierAccessComplianceResult result;
            try
            {
                result = await freeTierAccessComplianceService.EvaluateAccessForEnforcementAsync(userId, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to evaluate free-tier compliance for user {UserId}", userId);
                continue;
            }

            if (!result.IsApplicable || result.IsChannelSubscribed)
                continue;

            usersById.TryGetValue(userId, out var user);
            linksByUserId.TryGetValue(userId, out var links);
            links ??= [];

            var dto = new FreeTierEnforcementCandidateDto
            {
                UserId = userId,
                DisplayName = user?.DisplayName ?? user?.Email ?? $"User #{userId}",
                Email = string.IsNullOrWhiteSpace(user?.Email) ? null : user!.Email.Trim(),
                TelegramId = result.TelegramId,
                ActivePlanName = result.ActivePlanName,
                IsMergedAccount = result.IsMergedAccount,
                IsChannelSubscribed = result.IsChannelSubscribed,
                IdentityProviders = FormatIdentityProviders(links),
                IsConnected = true,
            };

            var session = connectedByUserId[userId];
            dto.VpnServerId = session.VpnServerId;
            dto.VpnServerName = serverNameById.GetValueOrDefault(session.VpnServerId);
            dto.CommonName = session.CommonName;
            dto.ConnectedSince = session.ConnectedSince;
            candidates.Add(dto);
        }

        return new GetFreeTierEnforcementCandidatesResponse
        {
            Candidates = candidates,
            TotalCount = candidates.Count,
            ConnectedCount = candidates.Count,
        };
    }

    private async Task<GetFreeTierEnforcementCandidatesResponse> CollectAsync(
        Func<FreeTierAccessComplianceResult, bool> include,
        bool connectedOnly,
        CancellationToken ct)
    {
        var freeTierUserIds = await GetFreeTierUserIdsAsync(ct);
        if (freeTierUserIds.Count == 0)
            return new GetFreeTierEnforcementCandidatesResponse();

        var servers = await vpnServerQueryService.GetAll(ct: ct);
        var serverNameById = servers.ToDictionary(s => s.Id, s => s.ServerName);

        var connectedByServerAndCn = (await vpnServerClientQueryService.GetAllConnected(ct))
            .Where(c => !string.IsNullOrWhiteSpace(c.CommonName))
            .GroupBy(c => (c.VpnServerId, c.CommonName!), StringPairComparer.Instance)
            .ToDictionary(g => g.Key, g => g.First(), StringPairComparer.Instance);

        var usersById = await userQueryService.GetByIds(freeTierUserIds, ct);
        var linksByUserId = await userIdentityLinkQueryService.GetListByUserIds(freeTierUserIds, ct);

        var allExternalIds = linksByUserId.Values
            .SelectMany(links => links)
            .Select(l => l.ExternalId?.Trim())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Cast<string>()
            .ToList();
        var issuedByExternalId = await issuedOvpnFileQueryService.GetAllByExternalIds(allExternalIds, ct);

        var candidates = new List<FreeTierEnforcementCandidateDto>();

        foreach (var userId in freeTierUserIds)
        {
            ct.ThrowIfCancellationRequested();

            FreeTierAccessComplianceResult result;
            try
            {
                result = await freeTierAccessComplianceService.EvaluateAccessForEnforcementAsync(userId, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to evaluate free-tier compliance for user {UserId}", userId);
                continue;
            }

            if (!include(result))
                continue;

            usersById.TryGetValue(userId, out var user);
            linksByUserId.TryGetValue(userId, out var links);
            links ??= [];

            var dto = new FreeTierEnforcementCandidateDto
            {
                UserId = userId,
                DisplayName = user?.DisplayName ?? user?.Email ?? $"User #{userId}",
                Email = string.IsNullOrWhiteSpace(user?.Email) ? null : user!.Email.Trim(),
                TelegramId = result.TelegramId,
                ActivePlanName = result.ActivePlanName,
                IsMergedAccount = result.IsMergedAccount,
                IsChannelSubscribed = result.IsChannelSubscribed,
                IdentityProviders = FormatIdentityProviders(links),
            };

            TryAttachConnection(dto, links, issuedByExternalId, connectedByServerAndCn, serverNameById);
            if (connectedOnly && !dto.IsConnected)
                continue;

            candidates.Add(dto);
        }

        return new GetFreeTierEnforcementCandidatesResponse
        {
            Candidates = candidates,
            TotalCount = candidates.Count,
            ConnectedCount = candidates.Count(c => c.IsConnected),
        };
    }

    private async Task<HashSet<int>> GetFreeTierUserIdsAsync(CancellationToken ct)
    {
        var plans = await quotaPlanQueryService.GetAll(ct);
        var freeDefaultPlanIds = plans
            .Where(p => QuotaPlanNames.IsFreeOrDefault(p.Name))
            .Select(p => p.Id)
            .ToHashSet();

        if (freeDefaultPlanIds.Count == 0)
            return [];

        var activeAssignments = await userQuotaPlanQueryService.GetAllActive(ct);
        return activeAssignments
            .Where(a => freeDefaultPlanIds.Contains(a.QuotaPlanId))
            .Select(a => a.UserId)
            .ToHashSet();
    }

    private async Task<Dictionary<VpnServerClient, int>> ResolveConnectedUserIdsAsync(
        IReadOnlyList<VpnServerClient> connected,
        CancellationToken ct)
    {
        var result = new Dictionary<VpnServerClient, int>(ReferenceEqualityComparer.Instance);

        var externalIds = connected
            .Select(c => c.ExternalId?.Trim())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Cast<string>()
            .ToList();

        var usersByExternalId = await userQueryService.GetByExternalIds(externalIds, ct);

        var unresolved = new List<VpnServerClient>();
        foreach (var client in connected)
        {
            if (client.UserId is > 0)
            {
                result[client] = client.UserId.Value;
                continue;
            }

            var externalId = client.ExternalId?.Trim();
            if (!string.IsNullOrWhiteSpace(externalId) &&
                usersByExternalId.TryGetValue(externalId, out var user))
            {
                result[client] = user.Id;
                continue;
            }

            unresolved.Add(client);
        }

        if (unresolved.Count == 0)
            return result;

        var pairs = unresolved
            .Select(c => (c.VpnServerId, c.CommonName!))
            .Distinct()
            .ToList();
        var issued = await issuedOvpnFileQueryService.GetActiveByServerAndCommonNames(pairs, ct);
        var externalByPair = issued
            .Where(f => !string.IsNullOrWhiteSpace(f.ExternalId) && !string.IsNullOrWhiteSpace(f.CommonName))
            .GroupBy(f => (f.VpnServerId, f.CommonName!), StringPairComparer.Instance)
            .ToDictionary(g => g.Key, g => g.First().ExternalId!, StringPairComparer.Instance);

        var fallbackExternalIds = externalByPair.Values
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var fallbackUsers = await userQueryService.GetByExternalIds(fallbackExternalIds, ct);

        foreach (var client in unresolved)
        {
            if (!externalByPair.TryGetValue((client.VpnServerId, client.CommonName!), out var externalId))
                continue;
            if (!fallbackUsers.TryGetValue(externalId, out var user))
                continue;
            result[client] = user.Id;
        }

        return result;
    }

    internal static List<string> FormatIdentityProviders(IEnumerable<UserIdentityLink> links)
        => links
            .Select(l => l.Provider?.Trim().ToLowerInvariant())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(ProviderSortKey)
            .Cast<string>()
            .ToList();

    private static int ProviderSortKey(string? provider)
        => provider switch
        {
            "google" => 0,
            "local" => 1,
            "telegram" => 2,
            _ => 9,
        };

    private static void TryAttachConnection(
        FreeTierEnforcementCandidateDto dto,
        IReadOnlyList<UserIdentityLink> links,
        IReadOnlyDictionary<string, List<IssuedOvpnFile>> issuedByExternalId,
        IReadOnlyDictionary<(int VpnServerId, string CommonName), VpnServerClient> connectedByServerAndCn,
        IReadOnlyDictionary<int, string> serverNameById)
    {
        var externalIds = links
            .Select(l => l.ExternalId?.Trim())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Cast<string>();

        foreach (var externalId in externalIds)
        {
            if (!issuedByExternalId.TryGetValue(externalId, out var issuedFiles))
                continue;

            foreach (var file in issuedFiles)
            {
                if (string.IsNullOrWhiteSpace(file.CommonName))
                    continue;

                if (!connectedByServerAndCn.TryGetValue((file.VpnServerId, file.CommonName), out var connected))
                    continue;

                dto.IsConnected = true;
                dto.VpnServerId = file.VpnServerId;
                dto.VpnServerName = serverNameById.GetValueOrDefault(file.VpnServerId);
                dto.CommonName = file.CommonName;
                dto.ConnectedSince = connected.ConnectedSince;
                return;
            }
        }
    }

    public async Task<GetFreeTierDisconnectLogResponse> GetDisconnectLogAsync(
        GetFreeTierDisconnectLogRequest request, CancellationToken ct = default)
    {
        var page = await disconnectLogQueryService.Page(
            request.Page,
            request.PageSize,
            orderBy: q => q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id),
            ct: ct);

        var entries = page.Items.Select(x => new FreeTierDisconnectLogEntryDto
        {
            Id = x.Id,
            UserId = x.UserId,
            UserDisplayName = x.UserDisplayNameSnapshot,
            VpnServerId = x.VpnServerId,
            VpnServerName = x.VpnServerNameSnapshot,
            CommonName = x.CommonName,
            Reason = (DisconnectReason)x.Reason,
            InitiatedByUserId = x.InitiatedByUserId,
            RevokeRequested = x.RevokeRequested,
            RevokeSucceeded = x.RevokeSucceeded,
            KillSucceeded = x.KillSucceeded,
            ErrorMessage = x.ErrorMessage,
            CreatedAt = x.CreatedAt,
        }).ToList();

        return new GetFreeTierDisconnectLogResponse
        {
            Entries = new PagedResponse<FreeTierDisconnectLogEntryDto>
            {
                Page = page.Page,
                PageSize = page.PageSize,
                TotalCount = page.TotalCount,
                Items = entries,
            },
        };
    }

    private sealed class StringPairComparer : IEqualityComparer<(int VpnServerId, string CommonName)>
    {
        public static readonly StringPairComparer Instance = new();

        public bool Equals((int VpnServerId, string CommonName) x, (int VpnServerId, string CommonName) y)
            => x.VpnServerId == y.VpnServerId && string.Equals(x.CommonName, y.CommonName, StringComparison.Ordinal);

        public int GetHashCode((int VpnServerId, string CommonName) obj)
            => HashCode.Combine(obj.VpnServerId, obj.CommonName);
    }
}
