using One.Domain.Enums;
using One.Domain.DTOs.Logging;
using System.Runtime.CompilerServices;

namespace One.Shared.Services.Logging
{
    public interface IClientLogger
    {
        Task LogInfo(string message, string? action = null, string? route = null);
        Task LogWarning(string message, string? action = null, string? route = null);
        Task LogError(
            string message,
            string? action = null,
            string? route = null,
            Exception? ex = null,
            [CallerMemberName] string callerMember = "",
            [CallerFilePath] string callerFile = "",
            [CallerLineNumber] int callerLine = 0);
        Task LogCritical(
            string message,
            string? action = null,
            string? route = null,
            Exception? ex = null,
            [CallerMemberName] string callerMember = "",
            [CallerFilePath] string callerFile = "",
            [CallerLineNumber] int callerLine = 0);

        Task LogEvaluation(LogDto logDto); // Low level
    }
}
