using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using One.API.Services;
using One.Infrastructure.Interfaces;

namespace One.API.Tests;

public sealed class OneApiFactory : WebApplicationFactory<Program>
{
    public Mock<IAuthService> AuthService { get; } = new(MockBehavior.Strict);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:UserConnection"] = TestConnectionString("OneUsersTests"),
            ["ConnectionStrings:MainConnection"] = TestConnectionString("OneMainTests"),
            ["JWT:Secret"] = new string('J', 64),
            ["JWT:ValidIssuer"] = "OneERP.Tests",
            ["JWT:ValidAudience"] = "OneERP.Tests",
            ["Security:OtpPepper"] = new string('O', 64),
            ["Security:RequireHttpsMetadata"] = "false",
            ["Authentication:AccessTokenMinutes"] = "30",
            ["Authentication:RefreshTokenDays"] = "14",
            ["Authentication:Lockout:MaxFailedAttempts"] = "5",
            ["Authentication:Lockout:Minutes"] = "15",
            ["Authentication:Otp:CodeLength"] = "6",
            ["Authentication:Otp:LifetimeMinutes"] = "5",
            ["Authentication:Otp:ResendCooldownSeconds"] = "60",
            ["Authentication:Otp:MaxAttempts"] = "5",
            ["Authentication:PasswordReset:ClientBaseUrl"] = "https://localhost",
            ["Authentication:PasswordReset:TokenMinutes"] = "20",
            ["Monitoring:ClientLogRetentionDays"] = "30",
            ["Monitoring:ServerLogRetentionDays"] = "14",
            ["Monitoring:DeleteBatchSize"] = "1000",
            ["Monitoring:MaxDeleteBatchesPerRun"] = "10",
            ["RateLimiting:WindowSeconds"] = "60",
            ["RateLimiting:PermitLimit"] = "120",
            ["RateLimiting:LoginPermitLimit"] = "10",
            ["RateLimiting:OtpRequestPermitLimit"] = "3",
            ["RateLimiting:OtpVerifyPermitLimit"] = "10",
            ["ReverseProxy:Enabled"] = "false"
        };
        foreach (var setting in settings)
            builder.UseSetting(setting.Key, setting.Value);
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(settings));
        builder.ConfigureServices(services =>
        {
            var backgroundWorkers = services
                .Where(descriptor =>
                    descriptor.ServiceType == typeof(IHostedService) &&
                    descriptor.ImplementationType is not null &&
                    (descriptor.ImplementationType == typeof(DataRetentionWorker) ||
                     descriptor.ImplementationType == typeof(SubscriptionStatusWorker)))
                .ToList();
            foreach (var worker in backgroundWorkers)
                services.Remove(worker);

            services.RemoveAll<IAuthService>();
            services.AddSingleton(AuthService.Object);
        });
    }

    public HttpClient CreateHttpsClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    private static string TestConnectionString(string database) =>
        $"Server=127.0.0.1;Database={database};Integrated Security=True;TrustServerCertificate=True";
}
