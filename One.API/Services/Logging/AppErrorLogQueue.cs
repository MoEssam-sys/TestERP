using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using One.Domain.Entities.Identity;
using One.Infrastructure.DbContext.UsersDbContext;

namespace One.API.Services.Logging;

public interface IAppErrorLogQueue
{
    bool TryWrite(AppErrorLogEntry entry);
}

public sealed record AppErrorLogEntry(
    string ReferenceNumber,
    Domain.Enums.LogSeverity Severity,
    int? CompanyId,
    string? UserId,
    string? UserName,
    string? Route,
    string? RequestMethod,
    string? FunctionName,
    string? SourceFile,
    int? LineNumber,
    int? StatusCode,
    string? CorrelationId,
    string Message,
    string? ExceptionType,
    string? StackTrace,
    DateTime OccurredAtUtc,
    DateTimeOffset OccurredAtLocal,
    string SourceCategory);

public sealed class AppErrorLogQueue(IServiceScopeFactory scopeFactory) : BackgroundService, IAppErrorLogQueue
{
    private readonly Channel<AppErrorLogEntry> channel = Channel.CreateBounded<AppErrorLogEntry>(
        new BoundedChannelOptions(1_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public bool TryWrite(AppErrorLogEntry entry) => channel.Writer.TryWrite(entry);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await channel.Reader.WaitToReadAsync(stoppingToken))
            {
                var batch = new List<AppErrorLogEntry>(100);
                while (batch.Count < 100 && channel.Reader.TryRead(out var entry))
                    batch.Add(entry);

                if (batch.Count > 0)
                    await PersistAsync(batch, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task PersistAsync(
        IReadOnlyCollection<AppErrorLogEntry> entries,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            var companyIds = entries
                .Where(entry => entry.CompanyId.HasValue)
                .Select(entry => entry.CompanyId!.Value)
                .Distinct()
                .ToArray();
            var companyNames = companyIds.Length == 0
                ? new Dictionary<int, string>()
                : await context.Companies
                    .AsNoTracking()
                    .Where(company => companyIds.Contains(company.Id))
                    .ToDictionaryAsync(company => company.Id, company => company.Name, cancellationToken);

            var receivedAtUtc = DateTime.UtcNow;
            context.AppLogs.AddRange(entries.Select(entry => new AppLog
            {
                ReferenceNumber = entry.ReferenceNumber,
                Severity = entry.Severity,
                Category = Domain.Enums.LogCategory.Exception,
                CompanyId = entry.CompanyId,
                CompanyName = entry.CompanyId.HasValue && companyNames.TryGetValue(entry.CompanyId.Value, out var name)
                    ? name
                    : null,
                UserId = entry.UserId,
                UserName = entry.UserName,
                Route = entry.Route,
                RequestMethod = entry.RequestMethod,
                Action = entry.SourceCategory,
                FunctionName = entry.FunctionName,
                SourceFile = entry.SourceFile,
                LineNumber = entry.LineNumber,
                StatusCode = entry.StatusCode,
                CorrelationId = entry.CorrelationId,
                Message = entry.Message,
                ExceptionType = entry.ExceptionType,
                StackTrace = entry.StackTrace,
                Platform = "Server",
                OccurredAtUtc = entry.OccurredAtUtc,
                OccurredAtLocal = entry.OccurredAtLocal,
                CreatedAtUtc = receivedAtUtc
            }));
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // Do not use ILogger here: this is its persistence sink and logging again would recurse.
            Console.Error.WriteLine($"[AppErrorLogQueue] Failed to persist error logs: {exception.Message}");
        }
    }
}
