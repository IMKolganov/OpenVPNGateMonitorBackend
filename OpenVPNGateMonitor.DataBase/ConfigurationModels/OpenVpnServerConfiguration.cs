using OpenVPNGateMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenVPNGateMonitor.DataBase.ConfigurationModels;

public class OpenVpnServerConfiguration : IEntityTypeConfiguration<OpenVpnServer>
{
    public void Configure(EntityTypeBuilder<OpenVpnServer> entity)
    {
        entity.HasKey(e => e.Id);
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
        entity.Property(e => e.LastUpdate)
            .IsRequired();
        entity.Property(e => e.CreateDate)
            .IsRequired();
    }
}