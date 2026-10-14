using System.Diagnostics;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using One.API.Security;

namespace One.API.Services.Logging;

public sealed class PersistentErrorLoggerProvider(
    IAppErrorLogQueue queue,
    IHttpContextAccessor httpContextAccessor) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) =>
        new PersistentErrorLogger(categoryName, queue, httpContextAccessor);

    public void Dispose()
    {
    }

    private sealed class PersistentErrorLogger(
        string categoryName,
        IAppErrorLogQueue queue,
        IHttpContextAccessor httpContextAccessor) : ILogger
    {
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel) || categoryName == typeof(AppErrorLogQueue).FullName)
                return;

            var httpContext = httpContextAccessor.HttpContext;
            var location = ExceptionLocation.Resolve(exception);
            var companyId = int.TryParse(
                httpContext?.User.FindFirst("companyId")?.Value,
                out var parsedCompanyId)
                    ? parsedCompanyId
                    : (int?)null;
            var message = SensitiveLogSanitizer.Sanitize(formatter(state, exception), 2_000);
            var stackTrace = exception is null
                ? null
                : SensitiveLogSanitizer.SanitizeOptional(exception.ToString(), 8_000);
            var clientPage = httpContext?.Request.Headers["X-One-Page"].FirstOrDefault();
            if (clientPage is not null &&
                (!clientPage.StartsWith("/", StringComparison.Ordinal) ||
                 clientPage.StartsWith("//", StringComparison.Ordinal)))
            {
                clientPage = null;
            }

            var referenceSuffix = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
            queue.TryWrite(new AppErrorLogEntry(
                ReferenceNumber: $"ONE-{referenceSuffix}",
                Severity: logLevel >= LogLevel.Critical
                    ? Domain.Enums.LogSeverity.Critical
                    : Domain.Enums.LogSeverity.Error,
                CompanyId: companyId,
                UserId: httpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                UserName: httpContext?.User.FindFirst(ClaimTypes.Name)?.Value,
                Route: SensitiveLogSanitizer.SanitizeOptional(
                    clientPage ?? httpContext?.Request.Path.Value,
                    500),
                RequestMethod: SensitiveLogSanitizer.SanitizeOptional(httpContext?.Request.Method, 16),
                FunctionName: SensitiveLogSanitizer.SanitizeOptional(location.FunctionName, 500),
                SourceFile: SensitiveLogSanitizer.SanitizeOptional(location.SourceFile, 260),
                LineNumber: location.LineNumber,
                StatusCode: exception is not null
                    ? StatusCodes.Status500InternalServerError
                    : httpContext?.Response.StatusCode,
                CorrelationId: SensitiveLogSanitizer.SanitizeOptional(httpContext?.TraceIdentifier, 100),
                Message: message,
                ExceptionType: SensitiveLogSanitizer.SanitizeOptional(exception?.GetType().FullName, 300),
                StackTrace: stackTrace,
                OccurredAtUtc: DateTime.UtcNow,
                OccurredAtLocal: DateTimeOffset.Now,
                SourceCategory: categoryName));
        }
    }

    private sealed record ExceptionLocation(string? FunctionName, string? SourceFile, int? LineNumber)
    {
        public static ExceptionLocation Resolve(Exception? exception)
        {
            var frames = exception is null
                ? new StackTrace(skipFrames: 2, fNeedFileInfo: true).GetFrames()
                : new StackTrace(exception, fNeedFileInfo: true).GetFrames();
            var frame = frames?.FirstOrDefault(candidate => candidate.GetFileLineNumber() > 0)
                ?? frames?.FirstOrDefault(candidate =>
                {
                    var typeName = candidate.GetMethod()?.DeclaringType?.FullName;
                    return typeName is not null &&
                           !typeName.StartsWith("Microsoft.Extensions.Logging", StringComparison.Ordinal) &&
                           !typeName.StartsWith(typeof(PersistentErrorLoggerProvider).FullName!, StringComparison.Ordinal);
                });
            var method = frame?.GetMethod();
            var functionName = method is null
                ? exception?.TargetSite?.Name
                : $"{method.DeclaringType?.FullName}.{method.Name}";
            var sourcePath = frame?.GetFileName();
            var lineNumber = frame?.GetFileLineNumber();
            return new ExceptionLocation(
                functionName,
                string.IsNullOrWhiteSpace(sourcePath) ? null : Path.GetFileName(sourcePath),
                lineNumber > 0 ? lineNumber : null);
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose()
        {
        }
    }
}
