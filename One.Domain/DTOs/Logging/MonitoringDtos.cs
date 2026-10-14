namespace One.Domain.DTOs.Logging;

public sealed record SystemHealthDto(
    string Status,
    bool IdentityDatabaseHealthy,
    bool BusinessDatabaseHealthy,
    int ActiveCompanies,
    int ActiveUsers,
    int ErrorsLast24Hours,
    DateTime CheckedAtUtc);
