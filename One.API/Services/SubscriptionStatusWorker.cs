using One.Infrastructure.Interfaces;

namespace One.API.Services
{
    public sealed class SubscriptionStatusWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<SubscriptionStatusWorker> logger) : BackgroundService
    {
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(RefreshInterval);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<ICompanySubscriptionService>();
                    var changed = await service.RefreshStatusesAsync(stoppingToken);
                    if (changed > 0)
                        logger.LogInformation("Refreshed {SubscriptionCount} subscription statuses.", changed);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Failed to refresh subscription statuses.");
                }

                try
                {
                    if (!await timer.WaitForNextTickAsync(stoppingToken))
                        break;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
