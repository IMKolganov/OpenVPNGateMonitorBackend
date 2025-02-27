using OpenVPNGateMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenVPNGateMonitor.DataBase.ConfigurationModels.Seeds;

namespace OpenVPNGateMonitor.DataBase.ConfigurationModels;

public class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Key)
            .IsRequired()
            .HasMaxLength(255);
        entity.Property(e => e.ValueType)
            .IsRequired()
            .HasMaxLength(50);
        entity.Property(e => e.StringValue)
            .HasMaxLength(255);
        entity.Property(e => e.IntValue);
        entity.Property(e => e.BoolValue);
        entity.Property(e => e.DoubleValue);
        entity.Property(e => e.DateTimeValue);

        entity.Property(e => e.LastUpdate)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.Property(e => e.CreateDate)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        
        entity.HasData(SettingSeedData.Data);
    }
}
