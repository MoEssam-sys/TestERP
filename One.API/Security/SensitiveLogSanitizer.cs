using System.Text.RegularExpressions;

namespace One.API.Security;

public static partial class SensitiveLogSanitizer
{
    public static string Sanitize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var sanitized = BearerToken().Replace(value, "Bearer [REDACTED]");
        sanitized = SensitiveJsonValue().Replace(sanitized, "$1[REDACTED]$3");
        return sanitized.Length <= maxLength ? sanitized : sanitized[..maxLength];
    }

    public static string? SanitizeOptional(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : Sanitize(value, maxLength);

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9._~+\-/]+=*", RegexOptions.IgnoreCase)]
    private static partial Regex BearerToken();

    [GeneratedRegex(@"((?:password|otp|token|secret)\s*[\""']?\s*[:=]\s*[\""']?)([^\s,;\""']+)([\""']?)", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveJsonValue();
}
