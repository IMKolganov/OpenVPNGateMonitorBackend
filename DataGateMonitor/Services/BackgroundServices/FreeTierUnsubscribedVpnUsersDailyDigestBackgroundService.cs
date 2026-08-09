using DataGateMonitor.Services.Users.Interfaces;

namespace DataGateMonitor.Services.BackgroundServices;

/// <summary>
/// Hourly tick that may send the once-per-UTC-day admin digest of Free/Default VPN users
/// who are online without a required Telegram channel subscription.
/// </summary>
public sealed class FreeTierUnsubscribedVpnUsersDailyDigestBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<FreeTierUnsubscribedVpnUsersDailyDigestBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish warming up, then check roughly once an hour.
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var digest = scope.ServiceProvider
                    .GetRequiredService<IFreeTierUnsubscribedVpnUsersDailyDigestService>();
                await digest.TrySendDailyDigestAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unsubscribed VPN-users daily digest iteration failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
