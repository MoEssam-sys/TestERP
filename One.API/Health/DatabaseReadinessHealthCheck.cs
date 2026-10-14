using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace One.API.Health;

public sealed class DatabaseReadinessHealthCheck(
    IConfiguration configuration,
    ILogger<DatabaseReadinessHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await CheckConnectionAsync(
                configuration.GetConnectionString("UserConnection")!,
                cancellationToken);
            await CheckConnectionAsync(
                configuration.GetConnectionString("MainConnection")!,
                cancellationToken);
            return HealthCheckResult.Healthy("Required databases are reachable.");
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException)
        {
            logger.LogError(exception, "A required database is unavailable.");
            return HealthCheckResult.Unhealthy(
                "A required database is unavailable.",
                exception);
        }
    }

    private static async Task CheckConnectionAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        command.CommandTimeout = 5;
        _ = await command.ExecuteScalarAsync(cancellationToken);
    }
}
