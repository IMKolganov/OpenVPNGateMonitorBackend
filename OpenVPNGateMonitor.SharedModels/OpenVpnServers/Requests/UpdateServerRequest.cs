﻿using System.ComponentModel.DataAnnotations;

namespace OpenVPNGateMonitor.SharedModels.OpenVpnServers.Requests;

public class UpdateServerRequest
{
    [Required(ErrorMessage = "Id is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "ServerId must be greater than 0.")]
    public int Id { get; set; }

    [Required(ErrorMessage = "Server name is required.")]
    public string ServerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Management IP is required.")]
    public string ManagementIp { get; set; } = string.Empty;

    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535.")]
    public int ManagementPort { get; set; }
    public string? Login { get; set; } = string.Empty;
    public string? Password { get; set; } = string.Empty;
    public bool IsOnline { get; set; } = false;
}