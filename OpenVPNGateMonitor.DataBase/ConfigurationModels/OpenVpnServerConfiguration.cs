using OpenVPNGateMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenVPNGateMonitor.DataBase.ConfigurationModels.Seeds;

namespace OpenVPNGateMonitor.DataBase.ConfigurationModels;

public class OpenVpnServerConfiguration : BaseEntityConfiguration<OpenVpnServer>
{
    public override void Configure(EntityTypeBuilder<OpenVpnServer> entity)
    {
        base.Configure(entity);
        entity.Property(e => e.ServerName)
            .IsRequired();
        entity.Property(e => e.ManagementIp)
            .IsRequired()
            .HasMaxLength(255);
        entity.Property(e => e.ManagementPort)
            .IsRequired();
        entity.Property(e => e.Login)
            .IsRequired()
            .HasMaxLength(50);
        entity.Property(e => e.Password)
            .IsRequired()
            .HasMaxLength(50);
        entity.Property(e => e.IsOnline);
        
        entity.HasData(OpenVpnServerSeedData.Data);
    }
}