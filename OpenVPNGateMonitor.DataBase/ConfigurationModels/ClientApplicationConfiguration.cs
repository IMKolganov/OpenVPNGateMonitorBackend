using OpenVPNGateMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenVPNGateMonitor.DataBase.ConfigurationModels;

public class ClientApplicationConfiguration : BaseEntityConfiguration<ClientApplication>
{
    public override void Configure(EntityTypeBuilder<ClientApplication> entity)
    {
        base.Configure(entity);
        entity.Property(e => e.ClientId)
            .IsRequired();
        entity.Property(e => e.Name)
            .IsRequired();
        entity.Property(e => e.ClientSecret)
            .IsRequired();
        entity.Property(e => e.IsRevoked)
            .IsRequired();
        entity.Property(e => e.IsSystem)
            .IsRequired();
    }
}