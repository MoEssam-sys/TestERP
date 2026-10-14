using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using One.Domain.Base;
using One.Domain.Constants;
using One.Domain.DTOs.Identity;
using One.Domain.Entities.Identity;
using One.Domain.Enums;
using One.Infrastructure.DbContext.UsersDbContext;
using One.Infrastructure.Interfaces;

namespace One.Application.Services.Subscriptions
{
    public sealed class PermissionService(
        UsersDbContext context,
        IMemoryCache cache,
        ILogger<PermissionService> logger) : IPermissionService
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public async Task<Result<EffectivePermissionsDto>> GetEffectiveAsync(
            ClaimsPrincipal user,
            CancellationToken cancellationToken = default)
        {
            if (user.Identity?.IsAuthenticated != true)
                return Result<EffectivePermissionsDto>.Fail(["Authentication is required."], 401);

            try
            {
                if (user.IsInRole(SystemRoles.SuperAdmin))
                    return Result<EffectivePermissionsDto>.Ok(await BuildSuperAdminAsync(cancellationToken));

                var userId = GetUserId(user);
                var companyId = GetCompanyId(user);
                if (userId is null || !companyId.HasValue)
                    return Result<EffectivePermissionsDto>.Fail(["Company context is missing."], 403);

                var version = await context.CompanyPermissionVersions
                    .AsNoTracking()
                    .Where(item => item.CompanyId == companyId.Value)
                    .Select(item => item.Version)
                    .FirstOrDefaultAsync(cancellationToken);
                if (version <= 0)
                    version = 1;

                var isCompanyAdmin = user.IsInRole(SystemRoles.CompanyAdmin);
                var cacheKey = $"permissions:{companyId.Value}:{userId}:{version}:{isCompanyAdmin}";
                if (cache.TryGetValue(cacheKey, out EffectivePermissionsDto? cached) && cached is not null)
                    return Result<EffectivePermissionsDto>.Ok(cached);

                var currentUser = await context.Users
                    .AsNoTracking()
                    .Where(item => item.Id == userId && item.CompanyId == companyId.Value)
                    .Select(item => new { item.IsActive, CompanyIsActive = item.Company!.IsActive })
                    .FirstOrDefaultAsync(cancellationToken);
                if (currentUser is null || !currentUser.IsActive || !currentUser.CompanyIsActive)
                {
                    return Result<EffectivePermissionsDto>.Ok(new EffectivePermissionsDto(
                        version,
                        false,
                        companyId,
                        null,
                        null,
                        [],
                        []));
                }

                var now = DateTime.UtcNow;
                var subscription = await context.CompanySubscriptions
                    .AsNoTracking()
                    .Where(item => item.CompanyId == companyId.Value)
                    .OrderByDescending(item => item.CreatedAtUtc)
                    .Select(item => new
                    {
                        item.Id,
                        item.Status,
                        item.StartsAtUtc,
                        item.EndsAtUtc
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (subscription is null ||
                    subscription.Status != SubscriptionStatus.Active ||
                    subscription.StartsAtUtc > now ||
                    subscription.EndsAtUtc <= now)
                {
                    var unavailable = new EffectivePermissionsDto(
                        version,
                        false,
                        companyId,
                        subscription?.Status,
                        subscription?.EndsAtUtc,
                        [],
                        []);
                    cache.Set(cacheKey, unavailable, CacheDuration);
                    return Result<EffectivePermissionsDto>.Ok(unavailable);
                }

                var companyPermissions = await context.CompanyEntitlements
                    .AsNoTracking()
                    .Where(item => item.CompanySubscriptionId == subscription.Id)
                    .Select(item => new PermissionSelectionDto(
                        item.AppPageAction.AppPage.Key,
                        item.AppPageAction.PermissionAction.Key))
                    .ToListAsync(cancellationToken);

                IReadOnlyList<PermissionSelectionDto> effectivePermissions = companyPermissions;
                if (!isCompanyAdmin)
                {
                    var userPermissions = await context.UserPermissions
                        .AsNoTracking()
                        .Where(item => item.CompanyId == companyId.Value && item.UserId == userId)
                        .Select(item => new PermissionSelectionDto(
                            item.AppPageAction.AppPage.Key,
                            item.AppPageAction.PermissionAction.Key))
                        .ToListAsync(cancellationToken);
                    var companySet = companyPermissions.ToHashSet();
                    effectivePermissions = userPermissions
                        .Where(companySet.Contains)
                        .Distinct()
                        .ToList();
                }

                var result = new EffectivePermissionsDto(
                    version,
                    false,
                    companyId,
                    subscription.Status,
                    subscription.EndsAtUtc,
                    effectivePermissions,
                    await BuildNavigationAsync(effectivePermissions, cancellationToken));
                cache.Set(cacheKey, result, CacheDuration);
                return Result<EffectivePermissionsDto>.Ok(result);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to calculate effective permissions.");
                return Result<EffectivePermissionsDto>.Fail(["Unable to load permissions."], 500);
            }
        }

        public async Task<bool> HasPermissionAsync(
            ClaimsPrincipal user,
            string pageKey,
            string actionKey,
            CancellationToken cancellationToken = default)
        {
            if (user.IsInRole(SystemRoles.SuperAdmin))
                return true;

            var result = await GetEffectiveAsync(user, cancellationToken);
            return result.Success && result.Data is not null && result.Data.Permissions.Contains(
                new PermissionSelectionDto(pageKey, actionKey));
        }

        public async Task<Result<UserPermissionEditorDto>> GetUserPermissionsAsync(
            string userId,
            ClaimsPrincipal actor,
            CancellationToken cancellationToken = default)
        {
            var access = await ResolveTargetAsync(userId, actor, cancellationToken);
            if (!access.Success || access.Target is null)
                return Result<UserPermissionEditorDto>.Fail([access.Error], access.StatusCode);

            return await BuildEditorAsync(access.Target, cancellationToken);
        }

        public async Task<Result<UserPermissionEditorDto>> ReplaceUserPermissionsAsync(
            string userId,
            ReplaceUserPermissionsRequest request,
            ClaimsPrincipal actor,
            CancellationToken cancellationToken = default)
        {
            var access = await ResolveTargetAsync(userId, actor, cancellationToken);
            if (!access.Success || access.Target is null)
                return Result<UserPermissionEditorDto>.Fail([access.Error], access.StatusCode);

            var currentVersion = await GetVersionAsync(access.Target.CompanyId, cancellationToken);
            if (request.Version != currentVersion)
                return Result<UserPermissionEditorDto>.Fail(["Permissions changed. Reload and try again."], 409);

            var resolution = await PermissionSelectionResolver.ResolveAsync(
                context,
                request.Permissions,
                requireAtLeastOne: false,
                cancellationToken);
            if (!resolution.Success)
                return Result<UserPermissionEditorDto>.Fail([resolution.Error]);

            var now = DateTime.UtcNow;
            var subscriptionId = await context.CompanySubscriptions
                .AsNoTracking()
                .Where(item =>
                    item.CompanyId == access.Target.CompanyId &&
                    item.Status == SubscriptionStatus.Active &&
                    item.StartsAtUtc <= now &&
                    item.EndsAtUtc > now)
                .Select(item => (long?)item.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (!subscriptionId.HasValue)
                return Result<UserPermissionEditorDto>.Fail(["Company does not have an active subscription."], 409);

            var companyKeys = await context.CompanyEntitlements
                .AsNoTracking()
                .Where(item => item.CompanySubscriptionId == subscriptionId.Value)
                .Select(item => new { item.AppPageId, item.PermissionActionId })
                .ToListAsync(cancellationToken);
            var companySet = companyKeys
                .Select(item => (item.AppPageId, item.PermissionActionId))
                .ToHashSet();
            if (resolution.Permissions.Any(permission =>
                !companySet.Contains((permission.AppPageId, permission.PermissionActionId))))
            {
                return Result<UserPermissionEditorDto>.Fail(
                    ["A user cannot receive permissions outside the company subscription."],
                    403);
            }

            var existing = await context.UserPermissions
                .Where(item => item.CompanyId == access.Target.CompanyId && item.UserId == userId)
                .ToListAsync(cancellationToken);
            var desired = resolution.Permissions
                .Select(item => (item.AppPageId, item.PermissionActionId))
                .ToHashSet();
            context.UserPermissions.RemoveRange(existing.Where(item =>
                !desired.Contains((item.AppPageId, item.PermissionActionId))));
            var existingKeys = existing
                .Select(item => (item.AppPageId, item.PermissionActionId))
                .ToHashSet();
            var actorId = GetUserId(actor) ?? string.Empty;
            foreach (var permission in resolution.Permissions.Where(item =>
                !existingKeys.Contains((item.AppPageId, item.PermissionActionId))))
            {
                context.UserPermissions.Add(new UserPermission
                {
                    CompanyId = access.Target.CompanyId,
                    UserId = userId,
                    AppPageId = permission.AppPageId,
                    PermissionActionId = permission.PermissionActionId,
                    CreatedAtUtc = now,
                    GrantedByUserId = actorId
                });
            }

            await PermissionVersionTracker.StageBumpAsync(context, access.Target.CompanyId, cancellationToken);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
                context.ChangeTracker.Clear();
                return await BuildEditorAsync(access.Target, cancellationToken);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                logger.LogInformation(exception, "Permission version conflict for company {CompanyId}.", access.Target.CompanyId);
                return Result<UserPermissionEditorDto>.Fail(["Permissions changed. Reload and try again."], 409);
            }
            catch (DbUpdateException exception)
            {
                logger.LogWarning(exception, "Failed to replace permissions for user {UserId}.", userId);
                return Result<UserPermissionEditorDto>.Fail(["User permissions conflict with current data."], 409);
            }
        }

        private async Task<EffectivePermissionsDto> BuildSuperAdminAsync(CancellationToken cancellationToken)
        {
            var permissions = await context.AppPageActions
                .AsNoTracking()
                .Where(item =>
                    item.AppPage.IsActive &&
                    item.AppPage.AppModule.IsActive &&
                    item.PermissionAction.IsActive)
                .Select(item => new PermissionSelectionDto(
                    item.AppPage.Key,
                    item.PermissionAction.Key))
                .ToListAsync(cancellationToken);
            return new EffectivePermissionsDto(
                0,
                true,
                null,
                null,
                null,
                permissions,
                await BuildNavigationAsync(permissions, cancellationToken));
        }

        private async Task<IReadOnlyList<NavigationModuleDto>> BuildNavigationAsync(
            IReadOnlyList<PermissionSelectionDto> permissions,
            CancellationToken cancellationToken)
        {
            var viewPages = permissions
                .Where(item => item.ActionKey == PermissionCatalogKeys.Actions.View)
                .Select(item => item.PageKey)
                .ToHashSet(StringComparer.Ordinal);
            var modules = await context.AppModules
                .AsNoTracking()
                .Where(item => item.IsActive)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Key)
                .Select(item => new
                {
                    item.Key,
                    item.NameResourceKey,
                    item.SortOrder,
                    Pages = item.Pages
                        .Where(page => page.IsActive && page.IsNavigationVisible)
                        .OrderBy(page => page.SortOrder)
                        .ThenBy(page => page.Key)
                        .Select(page => new NavigationPageDto(
                            page.Key,
                            page.NameResourceKey,
                            page.Route,
                            page.SortOrder))
                        .ToList()
                })
                .ToListAsync(cancellationToken);

            return modules
                .Select(module => new NavigationModuleDto(
                    module.Key,
                    module.NameResourceKey,
                    module.SortOrder,
                    module.Pages.Where(page => viewPages.Contains(page.Key)).ToList()))
                .Where(module => module.Pages.Count > 0)
                .ToList();
        }

        private async Task<Result<UserPermissionEditorDto>> BuildEditorAsync(
            TargetUser target,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var subscriptionId = await context.CompanySubscriptions
                .AsNoTracking()
                .Where(item =>
                    item.CompanyId == target.CompanyId &&
                    item.Status == SubscriptionStatus.Active &&
                    item.StartsAtUtc <= now &&
                    item.EndsAtUtc > now)
                .Select(item => (long?)item.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (!subscriptionId.HasValue)
                return Result<UserPermissionEditorDto>.Fail(["Company does not have an active subscription."], 409);

            var availableKeys = await context.CompanyEntitlements
                .AsNoTracking()
                .Where(item => item.CompanySubscriptionId == subscriptionId.Value)
                .Select(item => new { item.AppPageId, item.PermissionActionId })
                .ToListAsync(cancellationToken);
            var availableSet = availableKeys
                .Select(item => (item.AppPageId, item.PermissionActionId))
                .ToHashSet();
            var catalog = await context.AppModules
                .AsNoTracking()
                .AsSplitQuery()
                .Where(module => module.IsActive && module.IsAssignableToPlans)
                .Include(module => module.Pages)
                    .ThenInclude(page => page.Actions)
                    .ThenInclude(link => link.PermissionAction)
                .OrderBy(module => module.SortOrder)
                .ToListAsync(cancellationToken);
            var available = catalog
                .Select(module => new PermissionCatalogModuleDto(
                    module.Key,
                    module.NameResourceKey,
                    module.SortOrder,
                    module.IsAssignableToPlans,
                    module.Pages
                        .Where(page => page.IsActive && page.Actions.Any(link =>
                            availableSet.Contains((link.AppPageId, link.PermissionActionId))))
                        .OrderBy(page => page.SortOrder)
                        .Select(page => new PermissionCatalogPageDto(
                            page.Key,
                            page.NameResourceKey,
                            page.Route,
                            page.SortOrder,
                            page.IsNavigationVisible,
                            page.Actions
                                .Where(link =>
                                    link.PermissionAction.IsActive &&
                                    availableSet.Contains((link.AppPageId, link.PermissionActionId)))
                                .OrderBy(link => link.PermissionAction.SortOrder)
                                .Select(link => new PermissionCatalogActionDto(
                                    link.PermissionAction.Key,
                                    link.PermissionAction.NameResourceKey,
                                    link.PermissionAction.SortOrder))
                                .ToList()))
                        .ToList()))
                .Where(module => module.Pages.Count > 0)
                .ToList();
            var granted = await context.UserPermissions
                .AsNoTracking()
                .Where(item => item.CompanyId == target.CompanyId && item.UserId == target.UserId)
                .Select(item => new PermissionSelectionDto(
                    item.AppPageAction.AppPage.Key,
                    item.AppPageAction.PermissionAction.Key))
                .ToListAsync(cancellationToken);

            return Result<UserPermissionEditorDto>.Ok(new UserPermissionEditorDto(
                target.UserId,
                target.UserName,
                target.CompanyId,
                await GetVersionAsync(target.CompanyId, cancellationToken),
                available,
                granted));
        }

        private async Task<TargetResolution> ResolveTargetAsync(
            string userId,
            ClaimsPrincipal actor,
            CancellationToken cancellationToken)
        {
            var target = await context.Users
                .AsNoTracking()
                .Where(item => item.Id == userId && item.CompanyId.HasValue)
                .Select(item => new TargetUser(item.Id, item.UserName ?? item.Id, item.CompanyId!.Value))
                .FirstOrDefaultAsync(cancellationToken);
            if (target is null)
                return TargetResolution.Fail("User was not found.", 404);

            if (!actor.IsInRole(SystemRoles.SuperAdmin) && GetCompanyId(actor) != target.CompanyId)
                return TargetResolution.Fail("You cannot manage users from another company.", 403);

            var protectedRole = await (
                from userRole in context.UserRoles
                join role in context.Roles on userRole.RoleId equals role.Id
                where userRole.UserId == userId &&
                    (role.Name == SystemRoles.SuperAdmin || role.Name == SystemRoles.CompanyAdmin)
                select userRole.UserId)
                .AnyAsync(cancellationToken);
            if (protectedRole)
                return TargetResolution.Fail("Administrator permissions are inherited from the company subscription.", 409);

            return TargetResolution.Ok(target);
        }

        private async Task<long> GetVersionAsync(int companyId, CancellationToken cancellationToken)
        {
            var version = await context.CompanyPermissionVersions
                .AsNoTracking()
                .Where(item => item.CompanyId == companyId)
                .Select(item => item.Version)
                .FirstOrDefaultAsync(cancellationToken);
            return version <= 0 ? 1 : version;
        }

        private static string? GetUserId(ClaimsPrincipal user)
            => user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue("sub")
                ?? user.FindFirstValue("userId");

        private static int? GetCompanyId(ClaimsPrincipal user)
            => int.TryParse(user.FindFirstValue("companyId"), out var companyId) && companyId > 0
                ? companyId
                : null;

        private sealed record TargetUser(string UserId, string UserName, int CompanyId);

        private sealed record TargetResolution(bool Success, TargetUser? Target, string? Error, int StatusCode)
        {
            public static TargetResolution Ok(TargetUser target) => new(true, target, null, 200);

            public static TargetResolution Fail(string error, int statusCode)
                => new(false, null, error, statusCode);
        }
    }
}
