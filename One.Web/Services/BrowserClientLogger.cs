using One.Domain.DTOs.Logging;
using One.Domain.Enums;
using One.Shared.Services.Logging;
using One.Shared.Services.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using System.Runtime.CompilerServices;

namespace One.Web.Services;

public sealed class BrowserClientLogger(IServiceProvider serviceProvider) : IClientLogger
{
    public Task LogInfo(string message, string? action = null, string? route = null) =>
        LogEvaluation(Create(LogSeverity.Info, LogCategory.Activity, message, action, route));

    public Task LogWarning(string message, string? action = null, string? route = null) =>
        LogEvaluation(Create(LogSeverity.Warning, LogCategory.General, message, action, route));

    public Task LogError(
        string message,
        string? action = null,
        string? route = null,
        Exception? ex = null,
        [CallerMemberName] string callerMember = "",
        [CallerFilePath] string callerFile = "",
        [CallerLineNumber] int callerLine = 0) =>
        LogEvaluation(Create(LogSeverity.Error, ex is null ? LogCategory.UserError : LogCategory.Exception, message, action, route, ex, callerMember, callerFile, callerLine));

    public Task LogCritical(
        string message,
        string? action = null,
        string? route = null,
        Exception? ex = null,
        [CallerMemberName] string callerMember = "",
        [CallerFilePath] string callerFile = "",
        [CallerLineNumber] int callerLine = 0) =>
        LogEvaluation(Create(LogSeverity.Critical, LogCategory.System, message, action, route, ex, callerMember, callerFile, callerLine));

    public async Task LogEvaluation(LogDto logDto)
    {
        try
        {
            logDto.Platform = "Browser";
            logDto.ClientTimestampUtc = DateTime.UtcNow;
            logDto.ClientTimestampLocal = DateTimeOffset.Now;
            logDto.Route ??= serviceProvider.GetService<NavigationManager>() is { } navigation
                ? "/" + navigation.ToBaseRelativePath(navigation.Uri)
                : null;
            await serviceProvider
                .GetRequiredService<IOneApiClient>()
                .PostSilentlyAsync<object, LogDto>("api/Logs", logDto);
        }
        catch
        {
            // Client logging must never break the UI.
        }
    }

    private static LogDto Create(
        LogSeverity severity,
        LogCategory category,
        string message,
        string? action,
        string? route,
        Exception? ex = null,
        string? callerMember = null,
        string? callerFile = null,
        int callerLine = 0)
        {
            var log = new LogDto
            {
            Severity = severity,
            Category = category,
            Message = message,
            Action = action,
            Route = route,
            ExceptionType = ex?.GetType().Name,
            StackTrace = ex?.ToString(),
            FunctionName = callerMember,
            SourceFile = string.IsNullOrWhiteSpace(callerFile) ? null : Path.GetFileName(callerFile),
            LineNumber = callerLine > 0 ? callerLine : null
        };
        ExceptionDiagnostics.Enrich(log, ex);
        return log;
    }
}
