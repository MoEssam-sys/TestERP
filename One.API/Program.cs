using One.API.Configuration;
using One.API.Middleware;
using One.Application.Configuration;
using One.Infrastructure.Configuration;
using One.API.Hub;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, _, logger) =>
{
    logger
        .MinimumLevel.Information()
        .Enrich.FromLogContext()
        .WriteTo.Console();

    if (!context.HostingEnvironment.IsEnvironment("Testing"))
    {
        logger.WriteTo.File(
            "Logs/one-api-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: context.Configuration.GetValue<int>(
                "Monitoring:ServerLogRetentionDays",
                14),
            fileSizeLimitBytes: 10 * 1024 * 1024,
            rollOnFileSizeLimit: true);
    }
});

// Register all services via your extension methods
builder.Services.AddOneAPI(builder.Configuration, builder.Environment);
builder.Services.AddOneApplication();
builder.Services.AddOneInfrastructure();

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("ReverseProxy:Enabled"))
    app.UseForwardedHeaders();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

app.UseHttpsRedirection();
app.UseCors("MyCors");
app.UseRateLimiter();

// JWT pipeline (لو أنت مفعّله داخل AddOneAPI لازم دول هنا)
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<OneHub>("/OneHubs");
app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions { Predicate = check => check.Tags.Contains("live") })
    .DisableRateLimiting();
app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
    .DisableRateLimiting();

// Database migrations and catalog seeding are deployment concerns. Integration
// tests replace external dependencies and exercise the HTTP pipeline in isolation.
if (!app.Environment.IsEnvironment("Testing"))
    await ApiConfig.SeedRolesAsync(app.Services);

app.Run();

public partial class Program;
