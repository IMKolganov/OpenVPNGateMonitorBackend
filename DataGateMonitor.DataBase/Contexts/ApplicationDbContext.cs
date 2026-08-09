using DataGateMonitor.DataBase.ConfigurationModels;
using DataGateMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DataGateMonitor.DataBase.Contexts;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IConfiguration configuration)
    : DbContext(options)
{
    private readonly string _defaultSchema = (Environment.GetEnvironmentVariable("DB_DEFAULT_SCHEMA") 
                                              ?? configuration["DataBaseSettings:DefaultSchema"]) ?? "xgb_dashopnvpn";

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }
    
    public DbSet<VpnServerStatusLog> VpnServerStatusLogs { get; set; } = null!;
    public DbSet<VpnServerClient> VpnServerClients { get; set; } = null!;
    public DbSet<VpnServer> VpnServers { get; set; } = null!;
    public DbSet<IssuedOvpnFile> IssuedOvpnFiles { get; set; } = null!;
    public DbSet<IssuedOvpnFileToken> IssuedOvpnFileTokens { get; set; } = null!;
    public DbSet<IssuedXrayClientLink> IssuedXrayClientLinks { get; set; } = null!;
    public DbSet<IssuedXrayClientLinkToken> IssuedXrayClientLinkTokens { get; set; } = null!;
    public DbSet<VpnServerOvpnFileConfig> VpnServerOvpnFileConfigs { get; set; } = null!;
    public DbSet<ClientApplication> ClientApplications { get; set; } = null!;
    public DbSet<Setting> Settings { get; set; } = null!;
    public DbSet<VpnProfileNotificationGlobalPreference> VpnProfileNotificationGlobalPreferences { get; set; } = null!;
    public DbSet<VpnProfileNotificationPreference> VpnProfileNotificationPreferences { get; set; } = null!;
    public DbSet<TelegramBotUser> TelegramBotUsers { get; set; } = null!;
    public DbSet<TelegramBotUserProfilePhoto> TelegramBotUserProfilePhotos { get; set; } = null!;
    public DbSet<TelegramUserLanguagePreference> TelegramUserLanguagePreferences { get; set; } = null!;
    public DbSet<LocalizationText> LocalizationTexts { get; set; } = null!;
    public DbSet<IncomingMessageLog> IncomingMessageLogs { get; set; } = null!;
    public DbSet<VpnServerEventLog> VpnServerEventLogs { get; set; } = null!;
    public DbSet<VpnDnsQueryLog> VpnDnsQueryLogs { get; set; } = null!;
    public DbSet<VpnServerPiHoleConfig> VpnServerPiHoleConfigs { get; set; } = null!;
    public DbSet<VpnServerClientTraffic> VpnServerClientTraffics { get; set; } = null!;
    public DbSet<VpnServerClientTrafficDaily> VpnServerClientTrafficDailies { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<NotificationRecipient> NotificationRecipients { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<UserCredential> UserCredentials { get; set; } = null!;
    public DbSet<UserPasswordHistory> UserPasswordHistories { get; set; } = null!;
    public DbSet<UserIdentityLink> UserIdentityLinks { get; set; } = null!;
    public DbSet<MergedUserArchive> MergedUserArchives { get; set; } = null!;
    public DbSet<QuotaPlan> QuotaPlans { get; set; } = null!;
    public DbSet<UserQuotaPlan> UserQuotaPlans { get; set; } = null!;
    public DbSet<QuotaPlanAllowedServer> QuotaPlanAllowedServers { get; set; } = null!;
    public DbSet<Tag> Tags { get; set; } = null!;
    public DbSet<VpnServerTag> VpnServerTags { get; set; } = null!;
    public DbSet<VpnServerConflog> VpnServerConflogs { get; set; } = null!;
    public DbSet<SentEmailLog> SentEmailLogs { get; set; } = null!;
    public DbSet<EmailBroadcastTemplate> EmailBroadcastTemplates { get; set; } = null!;
    public DbSet<MobileCrashReport> MobileCrashReports { get; set; } = null!;
    public DbSet<WindowsCrashReport> WindowsCrashReports { get; set; } = null!;
    public DbSet<FreeTierDisconnectLog> FreeTierDisconnectLogs { get; set; } = null!;
    public DbSet<TvLoginSession> TvLoginSessions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_defaultSchema);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new VpnServerStatusLogConfiguration());
        modelBuilder.ApplyConfiguration(new VpnServerClientConfiguration());
        modelBuilder.ApplyConfiguration(new VpnServerConfiguration());
        modelBuilder.ApplyConfiguration(new IssuedOvpnFileConfiguration());
        modelBuilder.ApplyConfiguration(new IssuedOvpnFileTokenConfiguration());
        modelBuilder.ApplyConfiguration(new IssuedXrayClientLinkConfiguration());
        modelBuilder.ApplyConfiguration(new IssuedXrayClientLinkTokenConfiguration());
        modelBuilder.ApplyConfiguration(new VpnServerOvpnFileConfigConfiguration());
        modelBuilder.ApplyConfiguration(new ClientApplicationConfiguration());
        modelBuilder.ApplyConfiguration(new SettingConfiguration());
        modelBuilder.ApplyConfiguration(new VpnProfileNotificationGlobalPreferenceConfiguration());
        modelBuilder.ApplyConfiguration(new VpnProfileNotificationPreferenceConfiguration());
        modelBuilder.ApplyConfiguration(new TelegramBotUserConfiguration());
        modelBuilder.ApplyConfiguration(new TelegramBotUserProfilePhotoConfiguration());
        modelBuilder.ApplyConfiguration(new TelegramUserLanguagePreferenceConfiguration());
        modelBuilder.ApplyConfiguration(new LocalizationTextConfiguration());
        modelBuilder.ApplyConfiguration(new IncomingMessageLogConfiguration());
        modelBuilder.ApplyConfiguration(new VpnServerEventLogConfiguration());
        modelBuilder.ApplyConfiguration(new VpnDnsQueryLogConfiguration());
        modelBuilder.ApplyConfiguration(new VpnServerPiHoleConfigConfiguration());
        modelBuilder.ApplyConfiguration(new VpnServerClientTrafficConfiguration());
        modelBuilder.ApplyConfiguration(new VpnServerClientTrafficDailyConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationRecipientConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new UserCredentialConfiguration());
        modelBuilder.ApplyConfiguration(new UserIdentityLinkConfiguration());
        modelBuilder.ApplyConfiguration(new MergedUserArchiveConfiguration());
        modelBuilder.ApplyConfiguration(new QuotaPlanConfiguration());
        modelBuilder.ApplyConfiguration(new UserQuotaPlanConfiguration());
        modelBuilder.ApplyConfiguration(new QuotaPlanAllowedServerConfiguration());
        modelBuilder.ApplyConfiguration(new TagConfiguration());
        modelBuilder.ApplyConfiguration(new VpnServerTagConfiguration());
        modelBuilder.ApplyConfiguration(new VpnServerConflogConfiguration());
        modelBuilder.ApplyConfiguration(new SentEmailLogConfiguration());
        modelBuilder.ApplyConfiguration(new EmailBroadcastTemplateConfiguration());
        modelBuilder.ApplyConfiguration(new MobileCrashReportConfiguration());
        modelBuilder.ApplyConfiguration(new WindowsCrashReportConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new UserRoleConfiguration());
        modelBuilder.ApplyConfiguration(new DeviceConfiguration());
        modelBuilder.ApplyConfiguration(new UserRefreshTokenConfiguration());
        modelBuilder.ApplyConfiguration(new UserPasswordHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new FreeTierDisconnectLogConfiguration());
        modelBuilder.ApplyConfiguration(new TvLoginSessionConfiguration());
    }
    
    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is IBaseEntity)
            .ToList();

        foreach (var entry in entries)
        {
            var entity = (IBaseEntity)entry.Entity;

            if (entry.State == EntityState.Added)
            {
                var now = DateTimeOffset.UtcNow;
                entity.CreateDate = now;
                entity.LastUpdate = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(IBaseEntity.CreateDate)).IsModified = false;
                entity.LastUpdate = DateTimeOffset.UtcNow;
            }
        }
    }
}