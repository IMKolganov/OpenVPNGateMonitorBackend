using OpenVPNGateMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenVPNGateMonitor.DataBase.ConfigurationModels;

public class OpenVpnServerOvpnFileConfigConfiguration : IEntityTypeConfiguration<OpenVpnServerOvpnFileConfig>
{
    public void Configure(EntityTypeBuilder<OpenVpnServerOvpnFileConfig> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.ServerId)
            .IsRequired();
        entity.Property(e => e.VpnServerIp)
            .IsRequired()
            .HasMaxLength(255);
        entity.Property(e => e.VpnServerPort)
            .IsRequired();
        entity.Property(e => e.ConfigTemplate)
            .IsRequired()
            .HasColumnType("TEXT");
        entity.Property(e => e.LastUpdate)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        entity.Property(e => e.CreateDate)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}