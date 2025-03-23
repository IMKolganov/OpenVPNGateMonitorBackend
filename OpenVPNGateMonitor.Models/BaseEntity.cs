using System.ComponentModel.DataAnnotations;

namespace OpenVPNGateMonitor.Models;

public abstract class BaseEntity<TKey> : IBaseEntity
{
    [Key]
    public TKey Id { get; set; } = default!;

    public DateTime CreateDate { get; set; }
    
    public DateTime LastUpdate { get; set; }
}