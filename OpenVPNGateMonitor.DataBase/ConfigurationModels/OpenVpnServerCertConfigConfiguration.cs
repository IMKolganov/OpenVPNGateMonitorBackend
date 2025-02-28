using OpenVPNGateMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenVPNGateMonitor.DataBase.ConfigurationModels;

public class OpenVpnServerCertConfigConfiguration : BaseEntityConfiguration<OpenVpnServerCertConfig>
{
    public override void Configure(EntityTypeBuilder<OpenVpnServerCertConfig> entity)
    {
        base.Configure(entity);
        entity.Property(e => e.VpnServerId)
            .IsRequired();
        entity.Property(e => e.EasyRsaPath)
            .IsRequired()
            .HasMaxLength(255);
        entity.Property(e => e.OvpnFileDir)
            .IsRequired()
            .HasMaxLength(255);
        entity.Property(e => e.RevokedOvpnFilesDirPath)
            .IsRequired()
            .HasMaxLength(255);
        entity.Property(e => e.PkiPath)
            .IsRequired()
            .HasMaxLength(255);
        entity.Property(e => e.CaCertPath)
            .IsRequired()
            .HasMaxLength(255);
        entity.Property(e => e.TlsAuthKey)
            .IsRequired()
            .HasMaxLength(255);
        entity.Property(e => e.ServerRemoteIp)
            .IsRequired()
            .HasMaxLength(255);
        entity.Property(e => e.CrlPkiPath)
            .IsRequired()
            .HasMaxLength(255);
        entity.Property(e => e.CrlOpenvpnPath)
            .IsRequired()
            .HasMaxLength(255);
        entity.Property(e => e.StatusFilePath)
            .IsRequired()
            .HasMaxLength(255);
    }
}