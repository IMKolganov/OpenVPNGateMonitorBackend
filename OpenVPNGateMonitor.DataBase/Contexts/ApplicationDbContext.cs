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
        _defaultSchema = configuration["DataBaseSettings:DefaultSchema"] ?? throw new InvalidOperationException();
    }
    
    public DbSet<OpenVpnServerStatusLog> OpenVpnServerStatusLogs { get; set; } = null!;
    public DbSet<OpenVpnServerClient> OpenVpnServerClients { get; set; } = null!;
    public DbSet<OpenVpnServer> OpenVpnServers { get; set; } = null!;
    public DbSet<OpenVpnServerCertConfig> OpenVpnServerCertConfigs { get; set; } = null!;

    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_defaultSchema);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new OpenVpnServerStatusLogConfiguration());
        modelBuilder.ApplyConfiguration(new OpenVpnServerClientConfiguration());
        modelBuilder.ApplyConfiguration(new OpenVpnServerConfiguration());
        modelBuilder.ApplyConfiguration(new OpenVpnServerCertConfigConfiguration());

    }
    

}
