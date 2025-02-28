using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenVPNGateMonitor.Models;

namespace OpenVPNGateMonitor.DataBase.ConfigurationModels;

public abstract class BaseEntityConfiguration<T> : IEntityTypeConfiguration<T> where T : BaseEntity<int>
{
    public virtual void Configure(EntityTypeBuilder<T> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.CreateDate)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.Property(e => e.LastUpdate)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}