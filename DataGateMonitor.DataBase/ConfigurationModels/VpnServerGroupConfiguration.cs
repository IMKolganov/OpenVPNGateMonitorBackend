using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DataGateMonitor.Models;

namespace DataGateMonitor.DataBase.ConfigurationModels;

public class VpnServerGroupConfiguration : BaseEntityConfiguration<VpnServerGroup, int>
{
    public override void Configure(EntityTypeBuilder<VpnServerGroup> entity)
    {
        base.Configure(entity);

        entity.ToTable("VpnServerGroups");

        entity.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(64);

        entity.Property(e => e.SortOrder)
            .IsRequired()
            .HasDefaultValue(0);

        entity.HasIndex(e => e.Name)
            .IsUnique();

        entity.HasIndex(e => e.SortOrder);
    }
}
