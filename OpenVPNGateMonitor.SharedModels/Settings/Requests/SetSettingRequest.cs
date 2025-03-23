using System.ComponentModel.DataAnnotations;

namespace OpenVPNGateMonitor.SharedModels.Settings.Requests;

public class SetSettingRequest
{
    [Required(ErrorMessage = "Key is required.")]
    public string Key { get; set; } = string.Empty;

    [Required(ErrorMessage = "Value is required.")]
    public string Value { get; set; } = string.Empty;

    [Required(ErrorMessage = "Type is required.")]
    [RegularExpression("^(int|bool|double|datetime|string)$", ErrorMessage = "Type must be one of: int, bool, double, datetime, string.")]
    public string Type { get; set; } = string.Empty;
}