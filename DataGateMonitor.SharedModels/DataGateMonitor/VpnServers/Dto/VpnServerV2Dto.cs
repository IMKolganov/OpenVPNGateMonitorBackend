using System;
using System.Collections.Generic;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;

public class VpnServerV2Dto
{
	public int Id { get; set; }

	public VpnServerType ServerType { get; set; }

	public string ServerName { get; set; } = string.Empty;

	public bool IsOnline { get; set; }

	public bool IsDefault { get; set; }

	public string ApiUrl { get; set; } = string.Empty;

	public double? Latitude { get; set; }

	public double? Longitude { get; set; }

	public bool IsEnableWss { get; set; }

	public DateTimeOffset CreateDate { get; set; }

	public DateTimeOffset LastUpdate { get; set; }

	public bool IsDeleted { get; set; }

	public bool? DcoIsEnabled { get; set; }

	public List<string> Tags { get; set; } = new List<string>();

	public int? GroupId { get; set; }

	public string? GroupName { get; set; }

	public int SortOrder { get; set; }

	public List<QuotaPlanGroupDto> QuotaPlanGroups { get; set; } = new List<QuotaPlanGroupDto>();

	public bool IsAccessibleForUserQuotaPlan { get; set; }

	public DateTimeOffset? XrayClientsPolledAt { get; set; }

	public string? XrayClientsPollError { get; set; }

	public bool IsDisabled { get; set; }
}
