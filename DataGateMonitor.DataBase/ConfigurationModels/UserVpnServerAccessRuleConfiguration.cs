using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DataGateMonitor.Models;

namespace DataGateMonitor.DataBase.ConfigurationModels;

public class UserVpnServerAccessRuleConfiguration : BaseEntityConfiguration<UserVpnServerAccessRule, int>
{
    public override void Configure(EntityTypeBuilder<UserVpnServerAccessRule> entity)
    {
        base.Configure(entity);

        entity.Property(e => e.UserId).IsRequired();
        entity.Property(e => e.VpnServerId).IsRequired();
        entity.Property(e => e.Mode).IsRequired().HasConversion<int>();

        entity.HasIndex(e => new { e.UserId, e.VpnServerId }).IsUnique();
        entity.HasIndex(e => e.VpnServerId);
    }
}
