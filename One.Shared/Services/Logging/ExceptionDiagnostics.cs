using System.Diagnostics;
using One.Domain.DTOs.Logging;

namespace One.Shared.Services.Logging;

public static class ExceptionDiagnostics
{
    public static void Enrich(LogDto log, Exception? exception)
    {
        if (exception is null)
            return;

        log.ExceptionType ??= exception.GetType().FullName;
        log.StackTrace ??= exception.ToString();
        var frames = new StackTrace(exception, fNeedFileInfo: true).GetFrames();
        var frame = frames?.FirstOrDefault(item => item.GetFileLineNumber() > 0)
            ?? frames?.FirstOrDefault();
        var method = frame?.GetMethod() ?? exception.TargetSite;
        if (method is not null)
            log.FunctionName = $"{method.DeclaringType?.FullName}.{method.Name}";
        var path = frame?.GetFileName();
        if (!string.IsNullOrWhiteSpace(path))
            log.SourceFile = Path.GetFileName(path);
        var lineNumber = frame?.GetFileLineNumber() ?? 0;
        if (lineNumber > 0)
            log.LineNumber = lineNumber;
    }
}
