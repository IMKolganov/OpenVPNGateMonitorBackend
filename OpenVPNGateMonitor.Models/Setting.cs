using System.ComponentModel.DataAnnotations;

namespace OpenVPNGateMonitor.Models;

public class Setting : BaseEntity<int>
{
    [Key]
    [Required]
    public string Key { get; set; } = null!;
    public string? StringValue { get; set; }
    public int? IntValue { get; set; }
    public bool? BoolValue { get; set; }
    public double? DoubleValue { get; set; }
    public DateTime? DateTimeValue { get; set; }
    [Required]
    public string ValueType { get; set; } = null!; // "string", "int", "bool", "double", "datetime"
}
