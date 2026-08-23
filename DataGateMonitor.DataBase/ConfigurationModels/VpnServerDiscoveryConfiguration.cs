using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.DataBase.ConfigurationModels;

public class VpnServerDiscoveryConfiguration : BaseEntityConfiguration<VpnServerDiscovery, int>
{
    public override void Configure(EntityTypeBuilder<VpnServerDiscovery> entity)
    {
        base.Configure(entity);

        entity.ToTable("VpnServerDiscoveries");

        entity.Property(e => e.ApiUrl).IsRequired().HasMaxLength(512);
        entity.Property(e => e.SuggestedName).HasMaxLength(128);
        entity.Property(e => e.PublicIp).HasMaxLength(64);
        entity.Property(e => e.Version).HasMaxLength(64);
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.LastSeenUtc).IsRequired();
        entity.Property(e => e.RejectReason).HasMaxLength(512);

        entity.HasIndex(e => e.Status);
        entity.HasIndex(e => e.ApiUrl);
        entity.HasIndex(e => e.LastSeenUtc);

        entity.HasIndex(e => e.ApiUrl)
            .IsUnique()
            .HasDatabaseName("IX_VpnServerDiscoveries_ApiUrl_Pending")
            .HasFilter($"\"Status\" = {(int)VpnServerDiscoveryStatus.Pending}");
    }
}
