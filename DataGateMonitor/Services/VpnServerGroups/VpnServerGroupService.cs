using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query;
using DataGateMonitor.DataBase.Services.Query.VpnServerGroupTable;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerGroups.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerGroups.Requests;

namespace DataGateMonitor.Services.VpnServerGroups;

public class VpnServerGroupService(
    ICommandService<VpnServerGroup, int> groupCommandService,
    IVpnServerGroupQueryService groupQueryService,
    IQueryService<VpnServer, int> serverQueryService,
    ICommandService<VpnServer, int> serverCommandService,
    ITransactionRunner transactionRunner) : IVpnServerGroupService
{
    public async Task<List<VpnServerGroupDto>> GetAllAsync(CancellationToken ct = default)
    {
        var groups = await groupQueryService.GetAll(ct);
        var servers = await serverQueryService.Where(
            predicate: s => !s.IsDeleted && s.VpnServerGroupId != null,
            orderBy: q => q.OrderBy(s => s.SortOrder).ThenBy(s => s.Id),
            ct: ct);

        var byGroup = servers
            .GroupBy(s => s.VpnServerGroupId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(s => s.Id).ToList());

        return groups.Select(g => ToDto(g, byGroup.GetValueOrDefault(g.Id, []))).ToList();
    }

    public async Task<VpnServerGroupDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var group = await groupQueryService.GetById(id, ct);
        if (group is null) return null;

        var serverIds = await GetOrderedServerIdsAsync(id, ct);
        return ToDto(group, serverIds);
    }

    public async Task<VpnServerGroupDto> CreateAsync(string name, CancellationToken ct = default)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException("Group name is required.");

        var existing = await groupQueryService.GetByName(trimmed, ct);
        if (existing is not null)
            throw new InvalidOperationException("A group with this name already exists.");

        var all = await groupQueryService.GetAll(ct);
        var nextOrder = all.Count == 0 ? 0 : all.Max(g => g.SortOrder) + 1;

        var created = await groupCommandService.Add(
            new VpnServerGroup { Name = trimmed, SortOrder = nextOrder },
            saveChanges: true,
            ct);

        return ToDto(created, []);
    }

    public async Task<VpnServerGroupDto> UpdateAsync(int id, string name, CancellationToken ct = default)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException("Group name is required.");

        var group = await groupQueryService.GetById(id, ct)
            ?? throw new InvalidOperationException("Group not found.");

        var clash = await groupQueryService.GetByName(trimmed, ct);
        if (clash is not null && clash.Id != id)
            throw new InvalidOperationException("A group with this name already exists.");

        group.Name = trimmed;
        await groupCommandService.Update(group, saveChanges: true, ct);

        var serverIds = await GetOrderedServerIdsAsync(id, ct);
        return ToDto(group, serverIds);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        _ = await groupQueryService.GetById(id, ct)
            ?? throw new InvalidOperationException("Group not found.");

        await transactionRunner.RunAsync(async innerCt =>
        {
            var members = await serverQueryService.Where(
                predicate: s => s.VpnServerGroupId == id,
                ct: innerCt);

            foreach (var server in members)
            {
                server.VpnServerGroupId = null;
                await serverCommandService.Update(server, saveChanges: false, innerCt);
            }

            await serverCommandService.SaveChanges(innerCt);
            await groupCommandService.DeleteById(id, innerCt);
        }, ct);
    }

    public async Task ReorderAsync(ReorderVpnServerGroupsRequest request, CancellationToken ct = default)
    {
        if (request.Items.Count == 0) return;

        await transactionRunner.RunAsync(async innerCt =>
        {
            foreach (var item in request.Items)
            {
                var group = await groupQueryService.GetById(item.GroupId, innerCt)
                    ?? throw new InvalidOperationException($"Group {item.GroupId} not found.");
                group.SortOrder = item.SortOrder;
                await groupCommandService.Update(group, saveChanges: false, innerCt);
            }

            await groupCommandService.SaveChanges(innerCt);
        }, ct);
    }

    public async Task<VpnServerGroupDto> SetServersAsync(
        int groupId,
        SetVpnServerGroupServersRequest request,
        CancellationToken ct = default)
    {
        var group = await groupQueryService.GetById(groupId, ct)
            ?? throw new InvalidOperationException("Group not found.");

        await ApplyMembershipAsync(groupId, request.VpnServerIds, ct);
        return ToDto(group, request.VpnServerIds.Distinct().ToList());
    }

    public Task SetUngroupedServersAsync(SetVpnServerGroupServersRequest request, CancellationToken ct = default)
        => ApplyMembershipAsync(groupId: null, request.VpnServerIds, ct);

    private async Task ApplyMembershipAsync(int? groupId, List<int> orderedServerIds, CancellationToken ct)
    {
        var distinctOrdered = orderedServerIds.Distinct().ToList();

        await transactionRunner.RunAsync(async innerCt =>
        {
            if (groupId is int gid)
            {
                var previousMembers = await serverQueryService.Where(
                    predicate: s => s.VpnServerGroupId == gid,
                    ct: innerCt);

                foreach (var server in previousMembers.Where(s => !distinctOrdered.Contains(s.Id)))
                {
                    server.VpnServerGroupId = null;
                    await serverCommandService.Update(server, saveChanges: false, innerCt);
                }
            }

            for (var i = 0; i < distinctOrdered.Count; i++)
            {
                var serverId = distinctOrdered[i];
                var server = await serverQueryService.FindById(serverId, asNoTracking: false, ct: innerCt)
                    ?? throw new InvalidOperationException($"Server {serverId} not found.");

                server.VpnServerGroupId = groupId;
                server.SortOrder = i;
                await serverCommandService.Update(server, saveChanges: false, innerCt);
            }

            await serverCommandService.SaveChanges(innerCt);
        }, ct);
    }

    private async Task<List<int>> GetOrderedServerIdsAsync(int groupId, CancellationToken ct)
    {
        var servers = await serverQueryService.Where(
            predicate: s => !s.IsDeleted && s.VpnServerGroupId == groupId,
            orderBy: q => q.OrderBy(s => s.SortOrder).ThenBy(s => s.Id),
            ct: ct);
        return servers.Select(s => s.Id).ToList();
    }

    private static VpnServerGroupDto ToDto(VpnServerGroup group, List<int> serverIds) => new()
    {
        Id = group.Id,
        Name = group.Name,
        SortOrder = group.SortOrder,
        ServerIds = serverIds,
    };
}
