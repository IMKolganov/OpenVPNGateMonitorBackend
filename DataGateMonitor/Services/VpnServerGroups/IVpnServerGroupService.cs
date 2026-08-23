using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerGroups.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerGroups.Requests;

namespace DataGateMonitor.Services.VpnServerGroups;

public interface IVpnServerGroupService
{
    Task<List<VpnServerGroupDto>> GetAllAsync(CancellationToken ct = default);
    Task<VpnServerGroupDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<VpnServerGroupDto> CreateAsync(string name, CancellationToken ct = default);
    Task<VpnServerGroupDto> UpdateAsync(int id, string name, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task ReorderAsync(ReorderVpnServerGroupsRequest request, CancellationToken ct = default);
    Task<VpnServerGroupDto> SetServersAsync(int groupId, SetVpnServerGroupServersRequest request, CancellationToken ct = default);
    Task SetUngroupedServersAsync(SetVpnServerGroupServersRequest request, CancellationToken ct = default);
}
