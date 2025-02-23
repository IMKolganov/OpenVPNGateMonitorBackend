using OpenVPNGateMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenVPNGateMonitor.DataBase.ConfigurationModels;

public class RegisteredAppConfiguration : IEntityTypeConfiguration<RegisteredApp>
{
    public void Configure(EntityTypeBuilder<RegisteredApp> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.ClientId)
            .IsRequired();
        entity.Property(e => e.Name)
            .IsRequired();
        entity.Property(e => e.ClientSecret)
            .IsRequired();
        entity.Property(e => e.IsRevoked)
            .IsRequired();
        entity.Property(e => e.LastUpdate)
            .IsRequired();
        entity.Property(e => e.CreateDate)
            .IsRequired();
    }
}