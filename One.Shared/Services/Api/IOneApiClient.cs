using One.Domain.Base;

namespace One.Shared.Services.Api;

public interface IOneApiClient
{
    Task<bool> TryRefreshSessionAsync(CancellationToken cancellationToken = default);

    Task<Result<TResponse>> GetAsync<TResponse>(
        string route,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

    Task<Result<TResponse>> PostAsync<TResponse, TRequest>(
        string route,
        TRequest request,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

    Task<Result<TResponse>> PostSilentlyAsync<TResponse, TRequest>(
        string route,
        TRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<TResponse>> PutAsync<TResponse, TRequest>(
        string route,
        TRequest request,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

    Task<Result<TResponse>> DeleteAsync<TResponse, TRequest>(
        string route,
        TRequest? request = default,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);
}
