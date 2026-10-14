using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using One.Domain.Base;
using One.Domain.Constants;
using One.Domain.DTOs.Logging;
using One.Domain.Enums;
using One.Infrastructure.DbContext.UsersDbContext;

namespace One.API.Controllers;

[Route("api/admin-monitoring")]
[Authorize(Policy = AuthorizationPolicies.ManageCompanies)]
public sealed class AdminMonitoringController(
    UsersDbContext context,
    IConfiguration configuration,
    ILogger<AdminMonitoringController> logger) : BaseApiController
{
    [HttpGet("health")]
    public async Task<ActionResult<Result<SystemHealthDto>>> GetHealth(
        CancellationToken cancellationToken)
    {
        var identityHealthy = await CheckIdentityDatabaseAsync(cancellationToken);
        var businessHealthy = await CheckBusinessDatabaseAsync(cancellationToken);
        var activeCompanies = 0;
        var activeUsers = 0;
        var errorsLast24Hours = 0;

        if (identityHealthy)
        {
            var since = DateTime.UtcNow.AddHours(-24);
            activeCompanies = await context.Companies.AsNoTracking()
                .CountAsync(company => company.IsActive, cancellationToken);
            activeUsers = await context.Users.AsNoTracking()
                .CountAsync(user => user.IsActive, cancellationToken);
            errorsLast24Hours = await context.AppLogs.AsNoTracking()
                .CountAsync(log => log.Severity >= LogSeverity.Error && log.OccurredAtUtc >= since, cancellationToken);
        }

        var healthy = identityHealthy && businessHealthy;
        return Ok(Result<SystemHealthDto>.Ok(new SystemHealthDto(
            healthy ? "Healthy" : "Degraded",
            identityHealthy,
            businessHealthy,
            activeCompanies,
            activeUsers,
            errorsLast24Hours,
            DateTime.UtcNow)));
    }

    private async Task<bool> CheckIdentityDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            var healthy = await context.Database.CanConnectAsync(cancellationToken);
            if (!healthy)
                logger.LogError("The identity database health check failed.");
            return healthy;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The identity database health check failed.");
            return false;
        }
    }

    private async Task<bool> CheckBusinessDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqlConnection(
                configuration.GetConnectionString("MainConnection"));
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = 5;
            _ = await command.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException)
        {
            logger.LogError(exception, "The business database health check failed.");
            return false;
        }
    }
}
