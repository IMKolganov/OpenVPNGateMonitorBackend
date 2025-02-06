using OpenVPNGateMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenVPNGateMonitor.DataBase.ConfigurationModels;

public class OpenVpnServerStatusLogConfiguration : IEntityTypeConfiguration<OpenVpnServerStatusLog>
{
    public void Configure(EntityTypeBuilder<OpenVpnServerStatusLog> entity)
    {
        entity.HasKey(e => e.Id);
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
        entity.Property(e => e.LastUpdate)
            .IsRequired();
        entity.Property(e => e.CreateDate)
            .IsRequired();
    }
}