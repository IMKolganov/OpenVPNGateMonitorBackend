using System.ComponentModel.DataAnnotations;

namespace OpenVPNGateMonitor.Models;

public abstract class BaseEntity<T>
{
    [Key]
    public T Id { get; set; } = default!;

    public DateTime CreateDate { get; set; }
    
    public DateTime LastUpdate { get; set; }
}