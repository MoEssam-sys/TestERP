using System.Net.Http.Json;
using One.Domain.Base;
using One.Domain.Constants;
using One.Domain.DTOs.Identity;
using One.Shared.Services.Api;

namespace One.Shared.Services
{
    public sealed class EffectivePermissionState(IOneApiClient apiClient)
    {
        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private HashSet<PermissionSelectionDto> _permissionSet = [];
        private DateTime _loadedAtUtc;

        public EffectivePermissionsDto? Current { get; private set; }
        public bool IsLoaded => Current is not null;
        public event Action? Changed;

        public async Task<bool> RefreshAsync(bool force = false, CancellationToken cancellationToken = default)
        {
            if (!force && Current is not null && DateTime.UtcNow - _loadedAtUtc < TimeSpan.FromSeconds(30))
                return true;

            await _loadLock.WaitAsync(cancellationToken);
            try
            {
                if (!force && Current is not null && DateTime.UtcNow - _loadedAtUtc < TimeSpan.FromSeconds(30))
                    return true;

                var result = await apiClient.GetAsync<EffectivePermissionsDto>(
                    "api/permissions/effective",
                    cancellationToken: cancellationToken);
                if (!result.Success || result.Data is null)
                    return false;

                Current = result.Data;
                _permissionSet = result.Data.Permissions.ToHashSet();
                _loadedAtUtc = DateTime.UtcNow;
                Changed?.Invoke();
                return true;
            }
            finally
            {
                _loadLock.Release();
            }
        }

        public bool Can(string pageKey, string actionKey)
            => Current?.IsSuperAdmin == true ||
               _permissionSet.Contains(new PermissionSelectionDto(pageKey, actionKey));

        public bool CanView(string pageKey) => Can(pageKey, PermissionCatalogKeys.Actions.View);

        public void Clear()
        {
            Current = null;
            _permissionSet.Clear();
            _loadedAtUtc = default;
            Changed?.Invoke();
        }
    }
}
