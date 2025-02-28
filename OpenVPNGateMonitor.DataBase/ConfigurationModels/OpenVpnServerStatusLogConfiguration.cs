using OpenVPNGateMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenVPNGateMonitor.DataBase.ConfigurationModels;

public class OpenVpnServerStatusLogConfiguration : BaseEntityConfiguration<OpenVpnServerStatusLog>
{
    public override void Configure(EntityTypeBuilder<OpenVpnServerStatusLog> entity)
    {
        base.Configure(entity);
        entity.Property(e => e.VpnServerId)
            .IsRequired();
        entity.Property(e => e.SessionId)
            .IsRequired();
        entity.Property(e => e.UpSince)
            .IsRequired();
        entity.Property(e => e.ServerLocalIp)
            .HasMaxLength(255);
        entity.Property(e => e.ServerRemoteIp)
            .HasMaxLength(255);
        entity.Property(e => e.BytesIn)
            .IsRequired();
        entity.Property(e => e.BytesOut)
            .IsRequired();
        entity.Property(e => e.Version)
            .IsRequired();
    }
}