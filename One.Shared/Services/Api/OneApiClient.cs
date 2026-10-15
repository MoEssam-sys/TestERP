using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using One.Domain.Base;
using One.Domain.DTOs;
using One.Domain.DTOs.Identity;
using One.Shared.Services.Session;

namespace One.Shared.Services.Api;

public sealed class OneApiClient(
    HttpClient httpClient,
    IClientSession session,
    MessagesCenter messagesCenter) : IOneApiClient, IDisposable
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<Result<TResponse>> GetAsync<TResponse>(
        string route,
        string? errorMessage = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<TResponse, object>(HttpMethod.Get, route, null, errorMessage, true, cancellationToken);

    public Task<Result<TResponse>> PostAsync<TResponse, TRequest>(
        string route,
        TRequest request,
        string? errorMessage = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<TResponse, TRequest>(HttpMethod.Post, route, request, errorMessage, true, cancellationToken);

    public Task<Result<TResponse>> PostSilentlyAsync<TResponse, TRequest>(
        string route,
        TRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<TResponse, TRequest>(HttpMethod.Post, route, request, null, false, cancellationToken);

    public Task<Result<TResponse>> PutAsync<TResponse, TRequest>(
        string route,
        TRequest request,
        string? errorMessage = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<TResponse, TRequest>(HttpMethod.Put, route, request, errorMessage, true, cancellationToken);

    public Task<Result<TResponse>> DeleteAsync<TResponse, TRequest>(
        string route,
        TRequest? request = default,
        string? errorMessage = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<TResponse, TRequest>(HttpMethod.Delete, route, request, errorMessage, true, cancellationToken);

    public async Task<bool> TryRefreshSessionAsync(CancellationToken cancellationToken = default)
    {
        var requestedRefreshToken = session.RefreshToken;
        if (string.IsNullOrWhiteSpace(requestedRefreshToken))
            return false;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (session.IsAuthenticated &&
                !string.Equals(requestedRefreshToken, session.RefreshToken, StringComparison.Ordinal))
            {
                return true;
            }

            using var message = new HttpRequestMessage(HttpMethod.Post, "api/Authentication/Refresh")
            {
                Content = JsonContent.Create(
                    new RefreshTokenRequest { RefreshToken = requestedRefreshToken },
                    options: JsonOptions)
            };
            using var response = await httpClient.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
                    await session.SignOutAsync(cancellationToken);
                return false;
            }

            var result = ReadSuccess<AuthResponseDto>(raw, response.StatusCode);
            if (!result.Success || result.Data is null ||
                string.IsNullOrWhiteSpace(result.Data.Token) ||
                string.IsNullOrWhiteSpace(result.Data.RefreshToken))
            {
                await session.SignOutAsync(cancellationToken);
                return false;
            }

            await session.SignInAsync(
                result.Data.Token,
                result.Data.RefreshToken,
                cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return false;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<Result<TResponse>> SendAsync<TResponse, TRequest>(
        HttpMethod method,
        string route,
        TRequest? request,
        string? errorMessage,
        bool notifyError,
        CancellationToken cancellationToken)
    {
        if (session.RequiresRefresh &&
            !string.Equals(route, "api/Authentication/Refresh", StringComparison.OrdinalIgnoreCase))
        {
            await TryRefreshSessionAsync(cancellationToken);
        }

        var attempts = IsSafe(method) ? 3 : 1;
        var refreshAttempted = false;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using var message = new HttpRequestMessage(method, route);
                if (request is not null)
                    message.Content = JsonContent.Create(request, options: JsonOptions);

                using var response = await httpClient.SendAsync(
                    message,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                var raw = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    if (!refreshAttempted && await TryRefreshSessionAsync(cancellationToken))
                    {
                        refreshAttempted = true;
                        attempt--;
                        continue;
                    }

                    await session.SignOutAsync(cancellationToken);
                }

                if (response.IsSuccessStatusCode)
                    return ReadSuccess<TResponse>(raw, response.StatusCode);

                if (attempt < attempts && IsTransient(response.StatusCode))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
                    continue;
                }

                return await FailAsync<TResponse>(raw, response.StatusCode, errorMessage, notifyError);
            }
            catch (HttpRequestException) when (attempt < attempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < attempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
            {
                return await FailAsync<TResponse>(string.Empty, null, errorMessage ?? "Unable to contact the server.", notifyError);
            }
        }

        return await FailAsync<TResponse>(string.Empty, null, errorMessage ?? "Unable to contact the server.", notifyError);
    }

    private static Result<TResponse> ReadSuccess<TResponse>(string raw, HttpStatusCode statusCode)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Result<TResponse>.Ok(default!);

        using var document = JsonDocument.Parse(raw);
        if (TryProperty(document.RootElement, "success", out _))
        {
            var wrapped = JsonSerializer.Deserialize<Result<TResponse>>(raw, JsonOptions)
                ?? Result<TResponse>.Fail(["The server returned an invalid response."], (int)statusCode);
            wrapped.StatusCode ??= (int)statusCode;
            return wrapped;
        }

        var data = JsonSerializer.Deserialize<TResponse>(raw, JsonOptions);
        return Result<TResponse>.Ok(data!);
    }

    private async Task<Result<TResponse>> FailAsync<TResponse>(
        string raw,
        HttpStatusCode? statusCode,
        string? fallback,
        bool notifyError)
    {
        var errors = ExtractErrors(raw, fallback);
        if (notifyError)
            await messagesCenter.Error(errors);
        return Result<TResponse>.Fail(errors, statusCode.HasValue ? (int)statusCode.Value : 503);
    }

    private static List<string?> ExtractErrors(string raw, string? fallback)
    {
        var messages = new List<string?>();
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                using var document = JsonDocument.Parse(raw);
                var root = document.RootElement;
                if (TryProperty(root, "messages", out var list) && list.ValueKind == JsonValueKind.Array)
                {
                    messages.AddRange(list.EnumerateArray().Select(item => item.ToString()));
                }
                else if (TryProperty(root, "errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    foreach (var field in errors.EnumerateObject())
                    {
                        if (field.Value.ValueKind == JsonValueKind.Array)
                            messages.AddRange(field.Value.EnumerateArray().Select(item => item.ToString()));
                    }
                }

                foreach (var name in new[] { "message", "detail", "title" })
                {
                    if (TryProperty(root, name, out var value) && value.ValueKind == JsonValueKind.String)
                        messages.Add(value.GetString());
                }
            }
            catch (JsonException)
            {
                messages.Add(raw);
            }
        }

        if (messages.All(string.IsNullOrWhiteSpace))
            messages.Add(fallback ?? "The request could not be completed.");

        return messages
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryProperty(JsonElement root, string name, out JsonElement value)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static bool IsSafe(HttpMethod method) =>
        method == HttpMethod.Get || method == HttpMethod.Head || method == HttpMethod.Options;

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout || (int)statusCode >= 500;

    public void Dispose()
    {
        _refreshLock.Dispose();
        httpClient.Dispose();
    }
}
