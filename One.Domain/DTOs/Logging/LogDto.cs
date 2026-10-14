using One.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace One.Domain.DTOs.Logging
{
    public class LogDto
    {
        public LogSeverity Severity { get; set; }
        public LogCategory Category { get; set; }

        [MaxLength(500)]
        public string? Route { get; set; }
        [MaxLength(200)]
        public string? Action { get; set; }
        [MaxLength(500)]
        public string? FunctionName { get; set; }
        [MaxLength(260)]
        public string? SourceFile { get; set; }
        [Range(1, int.MaxValue)]
        public int? LineNumber { get; set; }
        [Required]
        [MaxLength(2000)]
        public string Message { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? ExceptionType { get; set; }
        [MaxLength(8000)]
        public string? StackTrace { get; set; }
        [MaxLength(128)]
        public string? Hash { get; set; }

        [MaxLength(100)]
        public string? Platform { get; set; }
        [MaxLength(300)]
        public string? DeviceModel { get; set; }
        [MaxLength(100)]
        public string? AppVersion { get; set; }

        [MaxLength(4000)]
        public string? MetadataJson { get; set; }

        // Client timestamp can be useful for offline sync ordering
        public DateTime ClientTimestampUtc { get; set; }
        public DateTimeOffset? ClientTimestampLocal { get; set; }
    }

    public sealed class AppLogQueryDto
    {
        [Range(1, 1_000_000)]
        public int PageNumber { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 20;

        public int? CompanyId { get; set; }
        public LogSeverity? Severity { get; set; }
        public LogCategory? Category { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }

        [MaxLength(200)]
        public string? Search { get; set; }
    }

    public sealed record AppLogDto(
        int Id,
        string ReferenceNumber,
        LogSeverity Severity,
        LogCategory Category,
        int? CompanyId,
        string? CompanyName,
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
        string? Platform,
        string? DeviceModel,
        string? AppVersion,
        DateTime OccurredAtUtc,
        DateTimeOffset OccurredAtLocal,
        DateTime CreatedAtUtc);
}
