using OpenVPNGateMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenVPNGateMonitor.DataBase.ConfigurationModels;

public class OpenVpnServerClientConfiguration : IEntityTypeConfiguration<OpenVpnServerClient>
{
    public void Configure(EntityTypeBuilder<OpenVpnServerClient> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.VpnServerId)
            .IsRequired();
        entity.Property(e => e.SessionId)
            .IsRequired();
        entity.Property(e => e.CommonName)
            .IsRequired()
            .HasMaxLength(255);
        entity.Property(e => e.RemoteIp)
            .IsRequired()
            .HasMaxLength(50);
        entity.Property(e => e.LocalIp)
            .IsRequired()
            .HasMaxLength(50);
        entity.Property(e => e.BytesReceived)
            .IsRequired();
        entity.Property(e => e.BytesSent)
            .IsRequired();
        entity.Property(e => e.ConnectedSince)
            .IsRequired();
        entity.Property(e => e.Username)
            .IsRequired()
            .HasMaxLength(255);
        entity.Property(e => e.Country)
            .HasMaxLength(100);
        entity.Property(e => e.Region)
            .HasMaxLength(100);
        entity.Property(e => e.City)
            .HasMaxLength(100);
        entity.Property(e => e.Latitude);
        entity.Property(e => e.Longitude);
        entity.Property(e => e.IsConnected);
        entity.Property(e => e.LastUpdate)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        entity.Property(e => e.CreateDate)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}