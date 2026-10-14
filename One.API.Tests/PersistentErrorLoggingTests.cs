using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using One.API.Services.Logging;

namespace One.API.Tests;

public sealed class PersistentErrorLoggingTests
{
    [Fact]
    public void LogError_Exception_CapturesTenantRequestAndSourceContext()
    {
        var queue = new CapturingQueue();
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-123"
        };
        context.Request.Path = "/api/branches";
        context.Request.Method = HttpMethods.Post;
        context.Request.Headers["X-One-Page"] = "/sales/branches";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("companyId", "42"),
            new Claim(ClaimTypes.NameIdentifier, "user-7"),
            new Claim(ClaimTypes.Name, "Test User")
        ], "Test"));
        using var provider = new PersistentErrorLoggerProvider(
            queue,
            new HttpContextAccessor { HttpContext = context });
        var logger = provider.CreateLogger("One.Application.TestService");

        var exception = Assert.Throws<InvalidOperationException>(ThrowKnownError);
        logger.LogError(exception, "The operation failed.");

        var entry = Assert.Single(queue.Entries);
        Assert.Equal(42, entry.CompanyId);
        Assert.Equal("user-7", entry.UserId);
        Assert.Equal("Test User", entry.UserName);
        Assert.Equal("/sales/branches", entry.Route);
        Assert.Equal(HttpMethods.Post, entry.RequestMethod);
        Assert.Equal("trace-123", entry.CorrelationId);
        Assert.Contains(nameof(ThrowKnownError), entry.FunctionName, StringComparison.Ordinal);
        Assert.Equal(nameof(PersistentErrorLoggingTests) + ".cs", entry.SourceFile);
        Assert.True(entry.LineNumber > 0);
        Assert.StartsWith("ONE-", entry.ReferenceNumber, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MonitoringEndpoints_AnonymousRequest_ReturnsUnauthorized()
    {
        using var factory = new OneApiFactory();
        using var client = factory.CreateHttpsClient();

        var logs = await client.GetAsync(
            "/api/Logs/admin",
            TestContext.Current.CancellationToken);
        var health = await client.GetAsync(
            "/api/admin-monitoring/health",
            TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, logs.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, health.StatusCode);
    }

    private static void ThrowKnownError() =>
        throw new InvalidOperationException("Expected test exception.");

    private sealed class CapturingQueue : IAppErrorLogQueue
    {
        public List<AppErrorLogEntry> Entries { get; } = [];

        public bool TryWrite(AppErrorLogEntry entry)
        {
            Entries.Add(entry);
            return true;
        }
    }
}
