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
        => CollectAsync(
            // Include grace-period users: still not subscribed, but temporarily treated as compliant.
            include: result => result.IsApplicable && !result.IsChannelSubscribed,
            connectedOnly: true,
            ct);

    private async Task<GetFreeTierEnforcementCandidatesResponse> CollectAsync(
        Func<FreeTierAccessComplianceResult, bool> include,
        bool connectedOnly,
        CancellationToken ct)
    {
        var plans = await quotaPlanQueryService.GetAll(ct);
        var freeDefaultPlanIds = plans
            .Where(p => QuotaPlanNames.IsFreeOrDefault(p.Name))
            .Select(p => p.Id)
            .ToHashSet();

        if (freeDefaultPlanIds.Count == 0)
            return new GetFreeTierEnforcementCandidatesResponse();

        var activeAssignments = await userQuotaPlanQueryService.GetAllActive(ct);
        var freeTierUserIds = activeAssignments
            .Where(a => freeDefaultPlanIds.Contains(a.QuotaPlanId))
            .Select(a => a.UserId)
            .Distinct()
            .ToList();

        if (freeTierUserIds.Count == 0)
            return new GetFreeTierEnforcementCandidatesResponse();

        var servers = await vpnServerQueryService.GetAll(ct: ct);
        var serverNameById = servers.ToDictionary(s => s.Id, s => s.ServerName);

        var connectedByServerAndCn = (await vpnServerClientQueryService.GetAllConnected(ct))
            .Where(c => !string.IsNullOrWhiteSpace(c.CommonName))
            .GroupBy(c => (c.VpnServerId, c.CommonName!), StringPairComparer.Instance)
            .ToDictionary(g => g.Key, g => g.First(), StringPairComparer.Instance);

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

            var user = await userQueryService.GetById(userId, ct);
            var dto = new FreeTierEnforcementCandidateDto
            {
                UserId = userId,
                DisplayName = user?.DisplayName ?? user?.Email ?? $"User #{userId}",
                Email = user?.Email,
                TelegramId = result.TelegramId,
                ActivePlanName = result.ActivePlanName,
                IsMergedAccount = result.IsMergedAccount,
                IsChannelSubscribed = result.IsChannelSubscribed,
            };

            await TryAttachConnectionAsync(dto, userId, connectedByServerAndCn, serverNameById, ct);
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

    private async Task TryAttachConnectionAsync(
        FreeTierEnforcementCandidateDto dto,
        int userId,
        IReadOnlyDictionary<(int VpnServerId, string CommonName), VpnServerClient> connectedByServerAndCn,
        IReadOnlyDictionary<int, string> serverNameById,
        CancellationToken ct)
    {
        var links = await userIdentityLinkQueryService.GetListByUserId(userId, ct);
        var externalIds = links
            .Select(l => l.ExternalId?.Trim())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Cast<string>()
            .ToList();

        foreach (var externalId in externalIds)
        {
            var issuedFiles = await issuedOvpnFileQueryService.GetAllByExternalId(externalId, ct);
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
