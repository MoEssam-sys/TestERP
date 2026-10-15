using System.Security.Claims;
using System.Text;
using System.Text.Json;
using One.Shared.Services.Platform;

namespace One.Shared.Services.Session;

public sealed class ClientSession(IClientStorage storage) : IClientSession
{
    private const string TokenKey = "one:session:access-token:v1";
    private const string RefreshTokenKey = "one:session:refresh-token:v1";
    private static readonly string[] LegacyTokenKeys = ["auth:token:v2", "superadmin:token:v1"];
    private readonly SemaphoreSlim _initializationLock = new(1, 1);

    public bool IsInitialized { get; private set; }
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTimeOffset? AccessTokenExpiresAtUtc { get; private set; }
    public bool RequiresRefresh =>
        RefreshToken is not null &&
        (!IsAuthenticated || AccessTokenExpiresAtUtc <= DateTimeOffset.UtcNow.AddMinutes(1));
    public ClaimsPrincipal User { get; private set; } = Anonymous();
    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;
    public event Action? Changed;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (IsInitialized)
            return;

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (IsInitialized)
                return;

            var token = await storage.GetAsync(TokenKey);
            var refreshToken = await storage.GetAsync(RefreshTokenKey);
            if (string.IsNullOrWhiteSpace(token))
                token = await ReadLegacyTokenAsync();

            if (!string.IsNullOrWhiteSpace(token) && TryCreatePrincipal(token, out var principal, out var expiration))
            {
                AccessToken = token;
                RefreshToken = refreshToken;
                AccessTokenExpiresAtUtc = expiration;
                User = principal;
                await storage.SetAsync(TokenKey, token);
            }
            else if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                AccessToken = null;
                RefreshToken = refreshToken;
                AccessTokenExpiresAtUtc = null;
                User = Anonymous();
                await storage.RemoveAsync(TokenKey);
                await RemoveLegacyTokensAsync();
            }
            else
            {
                await RemoveStoredTokensAsync();
            }

            IsInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }

        Changed?.Invoke();
    }

    public async Task SignInAsync(
        string accessToken,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryCreatePrincipal(accessToken, out var principal, out var expiration))
            throw new InvalidOperationException("The access token is invalid or expired.");
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new InvalidOperationException("The refresh token is missing.");

        await storage.SetAsync(TokenKey, accessToken);
        await storage.SetAsync(RefreshTokenKey, refreshToken);
        await RemoveLegacyTokensAsync();
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        AccessTokenExpiresAtUtc = expiration;
        User = principal;
        IsInitialized = true;
        Changed?.Invoke();
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await RemoveStoredTokensAsync();
        AccessToken = null;
        RefreshToken = null;
        AccessTokenExpiresAtUtc = null;
        User = Anonymous();
        IsInitialized = true;
        Changed?.Invoke();
    }

    public string? GetClaim(string claimType) =>
        User.FindFirst(claimType)?.Value;

    public bool IsInRole(string role) => User.IsInRole(role);

    private async Task<string?> ReadLegacyTokenAsync()
    {
        foreach (var key in LegacyTokenKeys)
        {
            var value = await storage.GetAsync(key);
            if (string.IsNullOrWhiteSpace(value))
                continue;

            try
            {
                using var document = JsonDocument.Parse(value);
                if (document.RootElement.TryGetProperty("Token", out var token) ||
                    document.RootElement.TryGetProperty("token", out token))
                {
                    return token.GetString();
                }
            }
            catch (JsonException)
            {
                if (value.Count(character => character == '.') == 2)
                    return value;
            }
        }

        return null;
    }

    private async Task RemoveStoredTokensAsync()
    {
        await storage.RemoveAsync(TokenKey);
        await storage.RemoveAsync(RefreshTokenKey);
        await RemoveLegacyTokensAsync();
    }

    private async Task RemoveLegacyTokensAsync()
    {
        foreach (var key in LegacyTokenKeys)
            await storage.RemoveAsync(key);
    }

    private static bool TryCreatePrincipal(
        string token,
        out ClaimsPrincipal principal,
        out DateTimeOffset expiration)
    {
        principal = Anonymous();
        expiration = default;
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
                return false;

            using var document = JsonDocument.Parse(DecodeBase64Url(parts[1]));
            if (!document.RootElement.TryGetProperty("exp", out var expirationElement) ||
                !expirationElement.TryGetInt64(out var expirationSeconds))
                return false;
            expiration = DateTimeOffset.FromUnixTimeSeconds(expirationSeconds);
            if (DateTimeOffset.UtcNow >= expiration)
                return false;

            var claims = new List<Claim>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var claimType = NormalizeClaimType(property.Name);
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var value in property.Value.EnumerateArray())
                        AddClaim(claims, claimType, value);
                }
                else
                {
                    AddClaim(claims, claimType, property.Value);
                }
            }

            principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt", ClaimTypes.Name, ClaimTypes.Role));
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static void AddClaim(List<Claim> claims, string claimType, JsonElement value)
    {
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        if (!string.IsNullOrWhiteSpace(text))
            claims.Add(new Claim(claimType, text));
    }

    private static string NormalizeClaimType(string claimType) => claimType switch
    {
        "role" or "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" => ClaimTypes.Role,
        "name" or "unique_name" or "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name" => ClaimTypes.Name,
        "email" or "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress" => ClaimTypes.Email,
        "nameid" or "sub" or "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier" => ClaimTypes.NameIdentifier,
        _ => claimType
    };

    private static string DecodeBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
    }

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());
}
