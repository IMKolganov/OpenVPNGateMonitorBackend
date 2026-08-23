using DataGateMonitor.SharedModels.DataGateMonitor.UserVpnServerAccessRules.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.UserVpnServerAccessRules.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.UserVpnServerAccessRules.Responses;

namespace DataGateMonitor.Services.VpnAccess;

public interface IUserVpnServerAccessRuleService
{
    Task<GetAllUserVpnServerAccessRulesResponse> GetPageAsync(GetAllUserVpnServerAccessRulesRequest request,
        CancellationToken ct);
    Task<UserVpnServerAccessRuleResponse?> GetByIdAsync(int id, CancellationToken ct);
    Task<List<UserVpnServerAccessRuleDto>> GetListByUserIdAsync(int userId, CancellationToken ct);
    Task<List<UserVpnServerAccessRuleDto>> GetListByVpnServerIdAsync(int vpnServerId, CancellationToken ct);
    Task<UserVpnServerAccessRuleResponse> CreateAsync(CreateOrUpdateUserVpnServerAccessRuleRequest request,
        CancellationToken ct);
    Task UpdateAsync(CreateOrUpdateUserVpnServerAccessRuleRequest request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}
