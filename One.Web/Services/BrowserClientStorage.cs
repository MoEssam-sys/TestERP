using Microsoft.JSInterop;
using One.Shared.Services.Platform;

namespace One.Web.Services;

public sealed class BrowserClientStorage(IJSRuntime jsRuntime) : IClientStorage
{
    public async Task<string?> GetAsync(string key) =>
        await jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", key);

    public async Task SetAsync(string key, string value) =>
        await jsRuntime.InvokeVoidAsync("sessionStorage.setItem", key, value);

    public async Task RemoveAsync(string key) =>
        await jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", key);
}
