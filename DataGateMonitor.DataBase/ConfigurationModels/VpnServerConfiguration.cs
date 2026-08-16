using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DataGateMonitor.Models;
using DataGateMonitor.DataBase.ConfigurationModels.Seeds;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.DataBase.ConfigurationModels;

public class VpnServerConfiguration : BaseEntityConfiguration<VpnServer, int>
{
    public override void Configure(EntityTypeBuilder<VpnServer> entity)
    {
        base.Configure(entity);

        entity.ToTable("VpnServers");

        entity.Property(e => e.ServerType)
            .HasDefaultValue(VpnServerType.OpenVpn);

        entity.Property(e => e.ServerName)
            .IsRequired();

        entity.Property(e => e.IsOnline);
        entity.Property(e => e.IsDefault);
        entity.Property(e => e.IsDisable);

        entity.Property(e => e.ApiUrl)
            .HasMaxLength(255);

        entity.Property(e => e.Latitude)
            .HasPrecision(9, 6); // Up to ~10cm accuracy

        entity.Property(e => e.Longitude)
            .HasPrecision(9, 6);
        
        entity.Property(e => e.IsEnableWss);
        entity.Property(e => e.IsPiHoleEnabled)
            .HasDefaultValue(false);
        entity.Property(e => e.IsDeleted)
            .HasDefaultValue(false);

        entity.Property(e => e.DcoIsEnabled);

        entity.Property(e => e.XrayClientsPollError).HasMaxLength(2000);

        entity.Property(e => e.VpnServerGroupId);

        entity.Property(e => e.SortOrder)
            .IsRequired()
            .HasDefaultValue(0);

        entity.HasOne<VpnServerGroup>()
            .WithMany()
            .HasForeignKey(e => e.VpnServerGroupId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        // Soft-deleted rows must not reserve the name — allow recreate with the same ServerName.
        entity.HasIndex(e => e.ServerName)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = FALSE");

        entity.HasIndex(e => e.IsOnline);

        entity.HasIndex(e => e.IsDefault);

        entity.HasIndex(e => e.IsDisable);
        
        entity.HasIndex(e => e.IsEnableWss);

        entity.HasIndex(e => new { e.Latitude, e.Longitude });
        entity.HasIndex(e => e.IsDeleted);

        entity.HasIndex(e => e.ServerType);

        entity.HasIndex(e => e.VpnServerGroupId);
        entity.HasIndex(e => new { e.VpnServerGroupId, e.SortOrder });

        // Seed data
        entity.HasData(VpnServerSeedData.Data);
    }
}