using System.Text;
using System.Text.Json;
using One.Shared.Services.Platform;
using One.Shared.Services.Session;

namespace One.Shared.Tests;

public sealed class ClientSessionTests
{
    [Fact]
    public async Task SignInAsync_ValidTokens_StoresOneSharedSession()
    {
        var storage = new MemoryClientStorage();
        var session = new ClientSession(storage);
        var token = Token(DateTimeOffset.UtcNow.AddMinutes(10), "CompanyAdmin");

        await session.SignInAsync(token, "refresh-token", TestContext.Current.CancellationToken);

        Assert.True(session.IsAuthenticated);
        Assert.True(session.IsInRole("CompanyAdmin"));
        Assert.Equal("refresh-token", session.RefreshToken);
        Assert.Equal(token, storage.Values["one:session:access-token:v1"]);
        Assert.Equal("refresh-token", storage.Values["one:session:refresh-token:v1"]);
    }

    [Fact]
    public async Task InitializeAsync_ExpiredAccessToken_PreservesRefreshForRotation()
    {
        var storage = new MemoryClientStorage(new Dictionary<string, string>
        {
            ["one:session:access-token:v1"] = Token(DateTimeOffset.UtcNow.AddMinutes(-1), "User"),
            ["one:session:refresh-token:v1"] = "refresh-token"
        });
        var session = new ClientSession(storage);

        await session.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.False(session.IsAuthenticated);
        Assert.True(session.RequiresRefresh);
        Assert.Null(session.AccessToken);
        Assert.Equal("refresh-token", session.RefreshToken);
        Assert.DoesNotContain("one:session:access-token:v1", storage.Values.Keys);
    }

    [Fact]
    public async Task SignOutAsync_ExistingSession_ClearsAccessRefreshAndLegacyTokens()
    {
        var storage = new MemoryClientStorage(new Dictionary<string, string>
        {
            ["one:session:access-token:v1"] = Token(DateTimeOffset.UtcNow.AddMinutes(10), "User"),
            ["one:session:refresh-token:v1"] = "refresh-token",
            ["auth:token:v2"] = "legacy-token",
            ["superadmin:token:v1"] = "legacy-admin-token"
        });
        var session = new ClientSession(storage);

        await session.SignOutAsync(TestContext.Current.CancellationToken);

        Assert.False(session.IsAuthenticated);
        Assert.Null(session.AccessToken);
        Assert.Null(session.RefreshToken);
        Assert.Empty(storage.Values);
    }

    private static string Token(DateTimeOffset expiration, string role)
    {
        var header = Encode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "none", typ = "JWT" }));
        var payload = Encode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            exp = expiration.ToUnixTimeSeconds(),
            userId = "test-user",
            companyId = "1",
            role
        }));
        return $"{header}.{payload}.test-signature";
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    private sealed class MemoryClientStorage : IClientStorage
    {
        public MemoryClientStorage(Dictionary<string, string>? values = null)
        {
            Values = values ?? [];
        }

        public Dictionary<string, string> Values { get; }

        public Task<string?> GetAsync(string key) =>
            Task.FromResult(Values.GetValueOrDefault(key));

        public Task SetAsync(string key, string value)
        {
            Values[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key)
        {
            Values.Remove(key);
            return Task.CompletedTask;
        }
    }
}
