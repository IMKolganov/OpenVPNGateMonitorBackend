using OpenVPNGateMonitor.DataBase.ConfigurationModels;
using OpenVPNGateMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace OpenVPNGateMonitor.DataBase.Contexts;

public class ApplicationDbContext : DbContext
{
    private readonly string _defaultSchema;
    
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IConfiguration configuration)
        : base(options)
    {
        _defaultSchema = (Environment.GetEnvironmentVariable("DB_DEFAULT_SCHEMA") 
                          ?? configuration["DataBaseSettings:DefaultSchema"]) ?? "public";
    }
    
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
    
    public DbSet<OpenVpnServerStatusLog> OpenVpnServerStatusLogs { get; set; } = null!;
    public DbSet<OpenVpnServerClient> OpenVpnServerClients { get; set; } = null!;
    public DbSet<OpenVpnServer> OpenVpnServers { get; set; } = null!;
    public DbSet<OpenVpnServerCertConfig> OpenVpnServerCertConfigs { get; set; } = null!;
    public DbSet<IssuedOvpnFile> IssuedOvpnFiles { get; set; } = null!;
    public DbSet<OpenVpnServerOvpnFileConfig> OpenVpnServerOvpnFileConfigs { get; set; } = null!;
    public DbSet<ClientApplication> ClientApplications { get; set; } = null!;
    public DbSet<Setting> Settings { get; set; } = null!;
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_defaultSchema);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new OpenVpnServerStatusLogConfiguration());
        modelBuilder.ApplyConfiguration(new OpenVpnServerClientConfiguration());
        modelBuilder.ApplyConfiguration(new OpenVpnServerConfiguration());
        modelBuilder.ApplyConfiguration(new OpenVpnServerCertConfigConfiguration());
        modelBuilder.ApplyConfiguration(new IssuedOvpnFileConfiguration());
        modelBuilder.ApplyConfiguration(new OpenVpnServerOvpnFileConfigConfiguration());
        modelBuilder.ApplyConfiguration(new ClientApplicationConfiguration());
        modelBuilder.ApplyConfiguration(new SettingConfiguration());
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
                var now = DateTime.UtcNow;
                entity.CreateDate = now;
                entity.LastUpdate = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(IBaseEntity.CreateDate)).IsModified = false;
                entity.LastUpdate = DateTime.UtcNow;
            }
        }
    }
}
