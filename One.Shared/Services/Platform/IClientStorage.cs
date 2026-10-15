namespace One.Shared.Services.Platform;

public interface IClientStorage
{
    Task<string?> GetAsync(string key);

    Task SetAsync(string key, string value);

    Task RemoveAsync(string key);
}
