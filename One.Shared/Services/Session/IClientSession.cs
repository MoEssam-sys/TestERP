using System.Security.Claims;

namespace One.Shared.Services.Session;

public interface IClientSession
{
    bool IsInitialized { get; }
    bool IsAuthenticated { get; }
    string? AccessToken { get; }
    string? RefreshToken { get; }
    DateTimeOffset? AccessTokenExpiresAtUtc { get; }
    bool RequiresRefresh { get; }
    ClaimsPrincipal User { get; }
    event Action? Changed;

    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task SignInAsync(
        string accessToken,
        string refreshToken,
        CancellationToken cancellationToken = default);
    Task SignOutAsync(CancellationToken cancellationToken = default);
    string? GetClaim(string claimType);
    bool IsInRole(string role);
}
