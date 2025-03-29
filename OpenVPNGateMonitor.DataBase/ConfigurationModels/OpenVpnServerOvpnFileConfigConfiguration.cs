using OpenVPNGateMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenVPNGateMonitor.DataBase.ConfigurationModels;

public class OpenVpnServerOvpnFileConfigConfiguration : BaseEntityConfiguration<OpenVpnServerOvpnFileConfig>
{
    public override void Configure(EntityTypeBuilder<OpenVpnServerOvpnFileConfig> entity)
    {
        base.Configure(entity);
        entity.Property(e => e.VpnServerId)
            .IsRequired();
        entity.Property(e => e.VpnServerIp)
            .IsRequired()
            .HasMaxLength(255);
        entity.Property(e => e.VpnServerPort)
            .IsRequired();
        entity.Property(e => e.ConfigTemplate)
            .IsRequired()
            .HasColumnType("TEXT");
    }
}