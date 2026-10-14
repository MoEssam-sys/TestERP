using Microsoft.EntityFrameworkCore;
using One.Infrastructure.DbContext.UsersDbContext;

namespace One.API.Services;

public sealed class DataRetentionWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DataRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunCleanupAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunCleanupAsync(stoppingToken);
    }

    private async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            var now = DateTime.UtcNow;
            var logRetentionDays = configuration.GetValue<int>("Monitoring:ClientLogRetentionDays", 30);
            var deleteBatchSize = configuration.GetValue<int>("Monitoring:DeleteBatchSize", 1000);
            var maxBatches = configuration.GetValue<int>("Monitoring:MaxDeleteBatchesPerRun", 10);

            var deletedLogs = 0;
            var deletedOtp = 0;
            var deletedSessions = 0;
            for (var batch = 0; batch < maxBatches; batch++)
            {
                var count = await context.AppLogs
                    .Where(log => log.CreatedAtUtc < now.AddDays(-logRetentionDays))
                    .OrderBy(log => log.Id)
                    .Take(deleteBatchSize)
                    .ExecuteDeleteAsync(cancellationToken);
                deletedLogs += count;
                if (count < deleteBatchSize)
                    break;
            }

            for (var batch = 0; batch < maxBatches; batch++)
            {
                var count = await context.OtpChallenges
                    .Where(challenge => challenge.ExpiresAtUtc < now.AddDays(-1))
                    .OrderBy(challenge => challenge.Id)
                    .Take(deleteBatchSize)
                    .ExecuteDeleteAsync(cancellationToken);
                deletedOtp += count;
                if (count < deleteBatchSize)
                    break;
            }

            for (var batch = 0; batch < maxBatches; batch++)
            {
                var count = await context.UserSessions
                    .Where(session =>
                        session.ExpiresAtUtc < now.AddDays(-7) ||
                        (session.RevokedAtUtc != null && session.RevokedAtUtc < now.AddDays(-7)))
                    .OrderBy(session => session.CreatedAtUtc)
                    .Take(deleteBatchSize)
                    .ExecuteDeleteAsync(cancellationToken);
                deletedSessions += count;
                if (count < deleteBatchSize)
                    break;
            }

            logger.LogInformation(
                "Retention cleanup removed {LogCount} client logs, {OtpCount} OTP challenges, and {SessionCount} sessions.",
                deletedLogs,
                deletedOtp,
                deletedSessions);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Retention cleanup failed.");
        }
    }
}
