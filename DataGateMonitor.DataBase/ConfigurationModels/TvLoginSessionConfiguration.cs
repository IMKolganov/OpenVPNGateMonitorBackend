using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DataGateMonitor.Models;

namespace DataGateMonitor.DataBase.ConfigurationModels;

public class TvLoginSessionConfiguration : BaseEntityConfiguration<TvLoginSession, Guid>
{
    public override void Configure(EntityTypeBuilder<TvLoginSession> entity)
    {
        base.Configure(entity);

        entity.ToTable("TvLoginSessions");

        entity.Property(e => e.UserCode)
            .IsRequired()
            .HasMaxLength(16);

        entity.Property(e => e.Status)
            .IsRequired();

        entity.Property(e => e.DeviceName)
            .HasMaxLength(128);

        entity.Property(e => e.Client)
            .HasMaxLength(64);

        entity.Property(e => e.ExpiresAt)
            .IsRequired();

        entity.Property(e => e.DeviceId)
            .HasMaxLength(128);

        entity.Property(e => e.UserAgent)
            .HasMaxLength(512);

        entity.HasIndex(e => e.UserCode);
        entity.HasIndex(e => e.Status);
        entity.HasIndex(e => e.ExpiresAt);
        entity.HasIndex(e => e.ApprovedUserId);
        entity.HasIndex(e => new { e.UserCode, e.Status });
        entity.HasIndex(e => new { e.ApprovedUserId, e.Status });

        // At most one open (Pending/Viewed) session may share a user code.
        entity.HasIndex(e => e.UserCode)
            .IsUnique()
            .HasDatabaseName("IX_TvLoginSessions_UserCode_Open")
            .HasFilter(
                $"\"Status\" IN ({(int)TvLoginSessionStatus.Pending}, {(int)TvLoginSessionStatus.Viewed})");
    }
}
