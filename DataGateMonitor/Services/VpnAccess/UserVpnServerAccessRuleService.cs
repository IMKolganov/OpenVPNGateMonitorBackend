using Mapster;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.DataGateMonitor.UserVpnServerAccessRules.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.UserVpnServerAccessRules.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.UserVpnServerAccessRules.Responses;

namespace DataGateMonitor.Services.VpnAccess;

public class UserVpnServerAccessRuleService(
    IUserVpnServerAccessRuleQueryService queryService,
    ICommandService<UserVpnServerAccessRule, int> commandService) : IUserVpnServerAccessRuleService
{
    public async Task<GetAllUserVpnServerAccessRulesResponse> GetPageAsync(
        GetAllUserVpnServerAccessRulesRequest request, CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : request.PageSize;
        if (pageSize > 500)
            pageSize = 500;

        var userId = request.UserId is > 0 ? request.UserId : null;
        var vpnServerId = request.VpnServerId is > 0 ? request.VpnServerId : null;

        var paged = await queryService.GetPage(page, pageSize, userId, vpnServerId, ct);

        return new GetAllUserVpnServerAccessRulesResponse
        {
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            Items = paged.Items.Adapt<List<UserVpnServerAccessRuleDto>>()
        };
    }

    public async Task<UserVpnServerAccessRuleResponse?> GetByIdAsync(int id, CancellationToken ct)
    {
        var entity = await queryService.GetById(id, ct);
        if (entity is null)
            return null;

        return new UserVpnServerAccessRuleResponse
        {
            UserVpnServerAccessRule = entity.Adapt<UserVpnServerAccessRuleDto>()
        };
    }

    public async Task<List<UserVpnServerAccessRuleDto>> GetListByUserIdAsync(int userId, CancellationToken ct)
    {
        var list = await queryService.GetListByUserId(userId, ct);
        return list.Adapt<List<UserVpnServerAccessRuleDto>>();
    }

    public async Task<List<UserVpnServerAccessRuleDto>> GetListByVpnServerIdAsync(int vpnServerId, CancellationToken ct)
    {
        var list = await queryService.GetListByVpnServerId(vpnServerId, ct);
        return list.Adapt<List<UserVpnServerAccessRuleDto>>();
    }

    /// <summary>Upserts the rule: re-granting or blocking the same pair flips the mode instead of failing.</summary>
    public async Task<UserVpnServerAccessRuleResponse> CreateAsync(
        CreateOrUpdateUserVpnServerAccessRuleRequest request, CancellationToken ct)
    {
        var existing = await queryService.GetByUserIdAndServerId(request.UserId, request.VpnServerId, ct);
        if (existing is not null)
        {
            if (existing.Mode != request.Mode)
            {
                existing.Mode = request.Mode;
                existing.LastUpdate = DateTimeOffset.UtcNow;
                await commandService.Update(existing, true, ct);
            }

            return new UserVpnServerAccessRuleResponse
            {
                UserVpnServerAccessRule = existing.Adapt<UserVpnServerAccessRuleDto>()
            };
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new UserVpnServerAccessRule
        {
            UserId = request.UserId,
            VpnServerId = request.VpnServerId,
            Mode = request.Mode,
            CreateDate = now,
            LastUpdate = now
        };

        entity = await commandService.Add(entity, true, ct);

        return new UserVpnServerAccessRuleResponse
        {
            UserVpnServerAccessRule = entity.Adapt<UserVpnServerAccessRuleDto>()
        };
    }

    public async Task UpdateAsync(CreateOrUpdateUserVpnServerAccessRuleRequest request, CancellationToken ct)
    {
        if (request.Id <= 0)
            throw new ArgumentException("Id is required for update.", nameof(request));

        var entity = await queryService.GetById(request.Id, ct)
            ?? throw new KeyNotFoundException($"UserVpnServerAccessRule {request.Id} not found.");

        var existing = await queryService.GetByUserIdAndServerId(request.UserId, request.VpnServerId, ct);
        if (existing is not null && existing.Id != request.Id)
            throw new InvalidOperationException("Another rule already exists for this UserId and VpnServerId.");

        entity.UserId = request.UserId;
        entity.VpnServerId = request.VpnServerId;
        entity.Mode = request.Mode;
        entity.LastUpdate = DateTimeOffset.UtcNow;

        await commandService.Update(entity, true, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var deleted = await commandService.DeleteById(id, ct);
        if (deleted == 0)
            throw new KeyNotFoundException($"UserVpnServerAccessRule {id} not found.");
    }
}
