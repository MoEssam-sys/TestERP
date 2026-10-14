using Microsoft.EntityFrameworkCore;
using One.Domain.DTOs.Identity;
using One.Infrastructure.DbContext.UsersDbContext;

namespace One.Application.Services.Subscriptions
{
    internal static class PermissionSelectionResolver
    {
        private const int MaximumSelections = 500;

        public static async Task<PermissionResolution> ResolveAsync(
            UsersDbContext context,
            IReadOnlyList<PermissionSelectionDto>? selections,
            bool requireAtLeastOne,
            CancellationToken cancellationToken)
        {
            if (selections is null)
                return PermissionResolution.Invalid("Permissions are required.");

            if (selections.Count > MaximumSelections)
                return PermissionResolution.Invalid($"A maximum of {MaximumSelections} permissions is allowed.");

            var requested = selections
                .Where(selection =>
                    !string.IsNullOrWhiteSpace(selection.PageKey) &&
                    !string.IsNullOrWhiteSpace(selection.ActionKey))
                .Select(selection => new PermissionSelectionDto(
                    selection.PageKey.Trim(),
                    selection.ActionKey.Trim()))
                .Distinct()
                .ToList();

            if (requireAtLeastOne && requested.Count == 0)
                return PermissionResolution.Invalid("At least one permission is required.");

            if (requested.Count != selections.Count)
                return PermissionResolution.Invalid("Permissions contain empty or duplicate entries.");

            var available = await context.AppPageActions
                .AsNoTracking()
                .Where(link =>
                    link.AppPage.IsActive &&
                    link.AppPage.AppModule.IsActive &&
                    link.AppPage.AppModule.IsAssignableToPlans &&
                    link.PermissionAction.IsActive)
                .Select(link => new ResolvedPermission(
                    link.AppPageId,
                    link.PermissionActionId,
                    link.AppPage.Key,
                    link.PermissionAction.Key))
                .ToListAsync(cancellationToken);

            var availableByKey = available.ToDictionary(
                permission => BuildKey(permission.PageKey, permission.ActionKey),
                StringComparer.Ordinal);
            var resolved = new List<ResolvedPermission>(requested.Count);

            foreach (var selection in requested)
            {
                if (!availableByKey.TryGetValue(
                    BuildKey(selection.PageKey, selection.ActionKey),
                    out var permission))
                {
                    return PermissionResolution.Invalid(
                        $"Permission '{selection.PageKey}:{selection.ActionKey}' is not assignable to plans.");
                }

                resolved.Add(permission);
            }

            return PermissionResolution.Valid(resolved);
        }

        private static string BuildKey(string pageKey, string actionKey)
            => $"{pageKey}\u001f{actionKey}";
    }

    internal sealed record ResolvedPermission(
        int AppPageId,
        int PermissionActionId,
        string PageKey,
        string ActionKey);

    internal sealed record PermissionResolution(
        bool Success,
        IReadOnlyList<ResolvedPermission> Permissions,
        string? Error)
    {
        public static PermissionResolution Valid(IReadOnlyList<ResolvedPermission> permissions)
            => new(true, permissions, null);

        public static PermissionResolution Invalid(string error)
            => new(false, [], error);
    }
}
