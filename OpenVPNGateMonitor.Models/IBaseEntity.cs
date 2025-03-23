namespace OpenVPNGateMonitor.Models;

public interface IBaseEntity
{
    DateTime CreateDate { get; set; }
    DateTime LastUpdate { get; set; }
}