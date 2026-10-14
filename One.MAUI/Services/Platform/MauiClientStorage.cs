using One.Shared.Services.Platform;

namespace One.MAUI.Services.Platform;

public sealed class MauiClientStorage : IClientStorage
{
    public Task<string?> GetAsync(string key) =>
        Microsoft.Maui.Storage.SecureStorage.GetAsync(key);

    public Task SetAsync(string key, string value) =>
        Microsoft.Maui.Storage.SecureStorage.SetAsync(key, value);

    public Task RemoveAsync(string key)
    {
        Microsoft.Maui.Storage.SecureStorage.Remove(key);
        return Task.CompletedTask;
    }
}
