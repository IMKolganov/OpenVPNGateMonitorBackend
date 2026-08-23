using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Responses;

namespace DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;

public class VpnServerWithStatusDto
{
	public VpnServerResponse VpnServerResponses { get; set; } = new VpnServerResponse();

	public VpnServerStatusLogResponse? VpnServerStatusLogResponse { get; set; }

	public int CountConnectedClients { get; set; }

	public int CountSessions { get; set; }

	public long TotalBytesIn { get; set; }

	public long TotalBytesOut { get; set; }

	/// <summary>Installed OpenVPN/Xray manager assembly version from node <c>/api/info</c>.</summary>
	public string? InstalledManagerVersion { get; set; }

	/// <summary>Latest manager version from the stack's GitHub Releases.</summary>
	public string? LatestManagerVersion { get; set; }

	/// <summary>True when <see cref="LatestManagerVersion"/> is strictly newer than <see cref="InstalledManagerVersion"/>.</summary>
	public bool IsManagerUpdateAvailable { get; set; }

	/// <summary>HTML URL of the latest GitHub Release, when known.</summary>
	public string? ManagerReleaseUrl { get; set; }
}
