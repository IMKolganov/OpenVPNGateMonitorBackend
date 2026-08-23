namespace DataGateMonitor.SharedModels.DataGateOpenVpnManager.OpenVpnProcess.Responses;

/// <summary>Result of OpenVPN daemon start / restart / kill / status on the node manager.</summary>
public class OpenVpnProcessStatusResponse
{
    public string Action { get; set; } = string.Empty;

    /// <summary>Whether the OpenVPN daemon process is currently alive.</summary>
    public bool IsRunning { get; set; }

    public int? Pid { get; set; }
    public string? ConfigPath { get; set; }
    public string? PidFilePath { get; set; }
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// True while start/restart/kill is executing on this node.
    /// Safe to poll from other clients — each VPN server has its own manager.
    /// </summary>
    public bool OperationInProgress { get; set; }

    /// <summary>
    /// High-level lifecycle: idle, running, stopped, starting, stopping, restarting, failed.
    /// </summary>
    public string Phase { get; set; } = "idle";

    /// <summary>Active mutate operation when <see cref="OperationInProgress"/>: start, restart, or kill.</summary>
    public string? CurrentOperation { get; set; }

    /// <summary>UTC time when the current mutate operation began (if any).</summary>
    public DateTime? OperationStartedAtUtc { get; set; }

    /// <summary>Last finished mutate operation: start, restart, or kill.</summary>
    public string? LastCompletedOperation { get; set; }

    public DateTime? LastCompletedAtUtc { get; set; }

    /// <summary>Last mutate failure message, cleared on the next successful mutate.</summary>
    public string? LastError { get; set; }
}
