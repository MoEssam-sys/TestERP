using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using One.Domain.Base;
using One.Domain.DTOs.Identity;
using One.Domain.Entities.Identity;
using One.Domain.Enums;
using One.Infrastructure.DbContext.UsersDbContext;
using One.Infrastructure.Interfaces;

namespace One.Application.Services.Subscriptions
{
    public sealed class CompanySubscriptionService(
        UsersDbContext context,
        ILogger<CompanySubscriptionService> logger) : ICompanySubscriptionService
    {
        private static readonly SubscriptionStatus[] CurrentStatuses =
        [
            SubscriptionStatus.Pending,
            SubscriptionStatus.Active,
            SubscriptionStatus.Suspended
        ];

        public async Task<Result<IReadOnlyList<CompanySubscriptionDto>>> GetByCompanyAsync(
            int companyId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var subscriptions = await SubscriptionQuery()
                    .Where(subscription => subscription.CompanyId == companyId)
                    .OrderByDescending(subscription => subscription.CreatedAtUtc)
                    .Take(100)
                    .ToListAsync(cancellationToken);

                return Result<IReadOnlyList<CompanySubscriptionDto>>.Ok(
                    subscriptions.Select(Map).ToList());
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to read subscriptions for company {CompanyId}.", companyId);
                return Result<IReadOnlyList<CompanySubscriptionDto>>.Fail(
                    ["Unable to load company subscriptions."],
                    500);
            }
        }

        public async Task<Result<CompanySubscriptionDto>> CreateAsync(
            CreateCompanySubscriptionRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            var company = await context.Companies.AsNoTracking().FirstOrDefaultAsync(
                item => item.Id == request.CompanyId,
                cancellationToken);
            if (company is null || !company.IsActive)
                return Result<CompanySubscriptionDto>.Fail(["Company was not found or is inactive."], 404);

            var plan = await context.SubscriptionPlans
                .AsNoTracking()
                .Include(item => item.Permissions)
                .FirstOrDefaultAsync(item => item.Id == request.SubscriptionPlanId, cancellationToken);
            if (plan is null || !plan.IsActive)
                return Result<CompanySubscriptionDto>.Fail(["Subscription plan was not found or is inactive."], 404);

            var permissionResult = await ResolveEntitlementsAsync(plan, request.Entitlements, cancellationToken);
            if (!permissionResult.Success)
                return Result<CompanySubscriptionDto>.Fail([permissionResult.Error]);

            var now = DateTime.UtcNow;
            var startsAtUtc = NormalizeUtc(request.StartsAtUtc ?? now);
            if (startsAtUtc > now.AddYears(1))
                return Result<CompanySubscriptionDto>.Fail(["Subscription start cannot be more than one year in the future."]);

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var currentSubscriptions = await context.CompanySubscriptions
                    .Where(item => item.CompanyId == request.CompanyId && CurrentStatuses.Contains(item.Status))
                    .ToListAsync(cancellationToken);

                foreach (var current in currentSubscriptions.Where(item => item.EndsAtUtc <= now))
                {
                    var previousStatus = current.Status;
                    current.Status = SubscriptionStatus.Expired;
                    current.UpdatedAtUtc = now;
                    current.UpdatedByUserId = currentUserId.ToString();
                    current.ChangeLogs.Add(CreateLog(
                        current,
                        SubscriptionChangeType.Expired,
                        previousStatus,
                        SubscriptionStatus.Expired,
                        "Subscription expired before a replacement was created.",
                        currentUserId.ToString(),
                        now));
                }

                if (currentSubscriptions.Any(item => item.EndsAtUtc > now))
                    return Result<CompanySubscriptionDto>.Fail(["Company already has a current subscription."], 409);

                var subscription = new CompanySubscription
                {
                    CompanyId = request.CompanyId,
                    SubscriptionPlanId = plan.Id,
                    StartsAtUtc = startsAtUtc,
                    EndsAtUtc = startsAtUtc.AddDays(plan.DurationInDays),
                    Status = startsAtUtc.AddDays(plan.DurationInDays) <= now
                        ? SubscriptionStatus.Expired
                        : startsAtUtc > now
                            ? SubscriptionStatus.Pending
                            : SubscriptionStatus.Active,
                    PriceSnapshot = plan.Price,
                    CurrencySnapshot = plan.Currency,
                    DurationInDaysSnapshot = plan.DurationInDays,
                    CreatedAtUtc = now,
                    CreatedByUserId = currentUserId.ToString()
                };

                ApplyEntitlements(
                    subscription,
                    permissionResult.Permissions,
                    permissionResult.IsCustomized,
                    now);
                subscription.ChangeLogs.Add(CreateLog(
                    subscription,
                    SubscriptionChangeType.Created,
                    null,
                    subscription.Status,
                    $"Created from plan '{plan.Code}'.",
                    currentUserId.ToString(),
                    now));

                context.CompanySubscriptions.Add(subscription);
                await PermissionVersionTracker.StageBumpAsync(context, request.CompanyId, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                context.ChangeTracker.Clear();
                return await GetByIdAsync(subscription.Id, cancellationToken);
            }
            catch (DbUpdateException exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                logger.LogWarning(exception, "Company subscription creation conflicted for company {CompanyId}.", request.CompanyId);
                return Result<CompanySubscriptionDto>.Fail(["Company already has a current subscription or the subscription data is invalid."], 409);
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                logger.LogError(exception, "Failed to create subscription for company {CompanyId}.", request.CompanyId);
                return Result<CompanySubscriptionDto>.Fail(["Unable to create the company subscription."], 500);
            }
        }

        public async Task<Result<CompanySubscriptionDto>> RenewAsync(
            long id,
            RenewCompanySubscriptionRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            if (!SubscriptionPlanService.TryDecodeRowVersion(request.RowVersion, out var rowVersion))
                return Result<CompanySubscriptionDto>.Fail(["Invalid subscription version."]);

            var subscription = await context.CompanySubscriptions
                .Include(item => item.Entitlements)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (subscription is null)
                return Result<CompanySubscriptionDto>.Fail(["Company subscription was not found."], 404);

            var planId = request.SubscriptionPlanId ?? subscription.SubscriptionPlanId;
            var plan = await context.SubscriptionPlans
                .AsNoTracking()
                .Include(item => item.Permissions)
                .FirstOrDefaultAsync(item => item.Id == planId, cancellationToken);
            if (plan is null || !plan.IsActive)
                return Result<CompanySubscriptionDto>.Fail(["Subscription plan was not found or is inactive."], 404);

            var permissionResult = await ResolveEntitlementsAsync(plan, request.Entitlements, cancellationToken);
            if (!permissionResult.Success)
                return Result<CompanySubscriptionDto>.Fail([permissionResult.Error]);

            var now = DateTime.UtcNow;
            var oldEnd = subscription.EndsAtUtc;
            var previousStatus = subscription.Status;
            var renewalBase = oldEnd > now ? oldEnd : now;

            context.Entry(subscription).Property(item => item.RowVersion).OriginalValue = rowVersion;
            subscription.SubscriptionPlanId = plan.Id;
            subscription.EndsAtUtc = renewalBase.AddDays(plan.DurationInDays);
            subscription.Status = subscription.StartsAtUtc > now
                ? SubscriptionStatus.Pending
                : SubscriptionStatus.Active;
            subscription.PriceSnapshot = plan.Price;
            subscription.CurrencySnapshot = plan.Currency;
            subscription.DurationInDaysSnapshot = plan.DurationInDays;
            subscription.UpdatedAtUtc = now;
            subscription.UpdatedByUserId = currentUserId.ToString();
            ApplyEntitlements(subscription, permissionResult.Permissions, permissionResult.IsCustomized, now);
            subscription.ChangeLogs.Add(CreateLog(
                subscription,
                SubscriptionChangeType.Renewed,
                previousStatus,
                subscription.Status,
                $"Renewed from {oldEnd:O} to {subscription.EndsAtUtc:O} using plan '{plan.Code}'.",
                currentUserId.ToString(),
                now));

            await PermissionVersionTracker.StageBumpAsync(context, subscription.CompanyId, cancellationToken);

            return await SaveAndReloadAsync(subscription, "renew", cancellationToken);
        }

        public async Task<Result<CompanySubscriptionDto>> ReplaceEntitlementsAsync(
            long id,
            ReplaceCompanyEntitlementsRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            if (!SubscriptionPlanService.TryDecodeRowVersion(request.RowVersion, out var rowVersion))
                return Result<CompanySubscriptionDto>.Fail(["Invalid subscription version."]);

            var resolution = await PermissionSelectionResolver.ResolveAsync(
                context,
                request.Entitlements,
                requireAtLeastOne: true,
                cancellationToken);
            if (!resolution.Success)
                return Result<CompanySubscriptionDto>.Fail([resolution.Error]);

            var subscription = await context.CompanySubscriptions
                .Include(item => item.Entitlements)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (subscription is null)
                return Result<CompanySubscriptionDto>.Fail(["Company subscription was not found."], 404);
            if (!CurrentStatuses.Contains(subscription.Status))
                return Result<CompanySubscriptionDto>.Fail(["Only a current subscription can be customized."], 409);

            var now = DateTime.UtcNow;
            context.Entry(subscription).Property(item => item.RowVersion).OriginalValue = rowVersion;
            ApplyEntitlements(subscription, resolution.Permissions, isCustomized: true, now);
            subscription.UpdatedAtUtc = now;
            subscription.UpdatedByUserId = currentUserId.ToString();
            subscription.ChangeLogs.Add(CreateLog(
                subscription,
                SubscriptionChangeType.EntitlementsChanged,
                subscription.Status,
                subscription.Status,
                $"Replaced company entitlements with {resolution.Permissions.Count} selections.",
                currentUserId.ToString(),
                now));

            await PermissionVersionTracker.StageBumpAsync(context, subscription.CompanyId, cancellationToken);

            return await SaveAndReloadAsync(subscription, "replace entitlements for", cancellationToken);
        }

        public Task<Result<CompanySubscriptionDto>> SuspendAsync(
            long id,
            ChangeSubscriptionStatusRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => ChangeStatusAsync(
                id,
                request,
                currentUserId,
                SubscriptionStatus.Active,
                SubscriptionStatus.Suspended,
                SubscriptionChangeType.Suspended,
                cancellationToken);

        public Task<Result<CompanySubscriptionDto>> ResumeAsync(
            long id,
            ChangeSubscriptionStatusRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => ChangeStatusAsync(
                id,
                request,
                currentUserId,
                SubscriptionStatus.Suspended,
                SubscriptionStatus.Active,
                SubscriptionChangeType.Resumed,
                cancellationToken);

        public async Task<Result<CompanySubscriptionDto>> CancelAsync(
            long id,
            ChangeSubscriptionStatusRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            if (!SubscriptionPlanService.TryDecodeRowVersion(request.RowVersion, out var rowVersion))
                return Result<CompanySubscriptionDto>.Fail(["Invalid subscription version."]);

            var subscription = await context.CompanySubscriptions.FirstOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);
            if (subscription is null)
                return Result<CompanySubscriptionDto>.Fail(["Company subscription was not found."], 404);
            if (!CurrentStatuses.Contains(subscription.Status))
                return Result<CompanySubscriptionDto>.Fail(["Only a current subscription can be cancelled."], 409);

            var now = DateTime.UtcNow;
            var previousStatus = subscription.Status;
            context.Entry(subscription).Property(item => item.RowVersion).OriginalValue = rowVersion;
            subscription.Status = SubscriptionStatus.Cancelled;
            subscription.UpdatedAtUtc = now;
            subscription.UpdatedByUserId = currentUserId.ToString();
            subscription.ChangeLogs.Add(CreateLog(
                subscription,
                SubscriptionChangeType.Cancelled,
                previousStatus,
                SubscriptionStatus.Cancelled,
                "Subscription was cancelled.",
                currentUserId.ToString(),
                now));

            await PermissionVersionTracker.StageBumpAsync(context, subscription.CompanyId, cancellationToken);

            return await SaveAndReloadAsync(subscription, "cancel", cancellationToken);
        }

        public async Task<int> RefreshStatusesAsync(CancellationToken cancellationToken = default)
        {
            const int batchSize = 500;
            var now = DateTime.UtcNow;
            var subscriptions = await context.CompanySubscriptions
                .Where(item => CurrentStatuses.Contains(item.Status) &&
                    (item.EndsAtUtc <= now ||
                     (item.Status == SubscriptionStatus.Pending && item.StartsAtUtc <= now)))
                .OrderBy(item => item.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            foreach (var subscription in subscriptions)
            {
                var previousStatus = subscription.Status;
                var changeType = subscription.EndsAtUtc <= now
                    ? SubscriptionChangeType.Expired
                    : SubscriptionChangeType.Activated;
                var newStatus = subscription.EndsAtUtc <= now
                    ? SubscriptionStatus.Expired
                    : SubscriptionStatus.Active;

                subscription.Status = newStatus;
                subscription.UpdatedAtUtc = now;
                subscription.UpdatedByUserId = null;
                subscription.ChangeLogs.Add(CreateLog(
                    subscription,
                    changeType,
                    previousStatus,
                    newStatus,
                    changeType == SubscriptionChangeType.Expired
                        ? "Subscription expired automatically."
                        : "Subscription activated automatically.",
                    null,
                    now));
            }

            await PermissionVersionTracker.StageBumpsAsync(
                context,
                subscriptions.Select(item => item.CompanyId),
                cancellationToken);

            if (subscriptions.Count > 0)
                await context.SaveChangesAsync(cancellationToken);

            return subscriptions.Count;
        }

        private async Task<Result<CompanySubscriptionDto>> ChangeStatusAsync(
            long id,
            ChangeSubscriptionStatusRequest request,
            Guid currentUserId,
            SubscriptionStatus expectedStatus,
            SubscriptionStatus newStatus,
            SubscriptionChangeType changeType,
            CancellationToken cancellationToken)
        {
            if (!SubscriptionPlanService.TryDecodeRowVersion(request.RowVersion, out var rowVersion))
                return Result<CompanySubscriptionDto>.Fail(["Invalid subscription version."]);

            var subscription = await context.CompanySubscriptions.FirstOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);
            if (subscription is null)
                return Result<CompanySubscriptionDto>.Fail(["Company subscription was not found."], 404);
            if (subscription.Status != expectedStatus)
                return Result<CompanySubscriptionDto>.Fail([$"Subscription must be {expectedStatus} before this operation."], 409);

            var now = DateTime.UtcNow;
            if (newStatus == SubscriptionStatus.Active && subscription.EndsAtUtc <= now)
                return Result<CompanySubscriptionDto>.Fail(["Expired subscription cannot be resumed; renew it instead."], 409);

            context.Entry(subscription).Property(item => item.RowVersion).OriginalValue = rowVersion;
            subscription.Status = newStatus;
            subscription.UpdatedAtUtc = now;
            subscription.UpdatedByUserId = currentUserId.ToString();
            subscription.ChangeLogs.Add(CreateLog(
                subscription,
                changeType,
                expectedStatus,
                newStatus,
                $"Subscription status changed from {expectedStatus} to {newStatus}.",
                currentUserId.ToString(),
                now));

            await PermissionVersionTracker.StageBumpAsync(context, subscription.CompanyId, cancellationToken);

            return await SaveAndReloadAsync(subscription, "change status for", cancellationToken);
        }

        private async Task<Result<CompanySubscriptionDto>> SaveAndReloadAsync(
            CompanySubscription subscription,
            string operation,
            CancellationToken cancellationToken)
        {
            try
            {
                await context.SaveChangesAsync(cancellationToken);
                var id = subscription.Id;
                context.ChangeTracker.Clear();
                return await GetByIdAsync(id, cancellationToken);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                logger.LogInformation(exception, "Company subscription {SubscriptionId} has a concurrency conflict.", subscription.Id);
                return Result<CompanySubscriptionDto>.Fail(["The subscription was changed by another user. Reload and try again."], 409);
            }
            catch (DbUpdateException exception)
            {
                logger.LogWarning(exception, "Failed to {Operation} subscription {SubscriptionId} because of a data conflict.", operation, subscription.Id);
                return Result<CompanySubscriptionDto>.Fail(["The subscription change conflicts with current data."], 409);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to {Operation} subscription {SubscriptionId}.", operation, subscription.Id);
                return Result<CompanySubscriptionDto>.Fail(["Unable to update the company subscription."], 500);
            }
        }

        private async Task<Result<CompanySubscriptionDto>> GetByIdAsync(
            long id,
            CancellationToken cancellationToken)
        {
            var subscription = await SubscriptionQuery().FirstOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);
            return subscription is null
                ? Result<CompanySubscriptionDto>.Fail(["Company subscription was not found."], 404)
                : Result<CompanySubscriptionDto>.Ok(Map(subscription));
        }

        private IQueryable<CompanySubscription> SubscriptionQuery()
            => context.CompanySubscriptions
                .AsNoTracking()
                .AsSplitQuery()
                .Include(subscription => subscription.SubscriptionPlan)
                .Include(subscription => subscription.Entitlements)
                    .ThenInclude(entitlement => entitlement.AppPageAction)
                    .ThenInclude(link => link.AppPage)
                .Include(subscription => subscription.Entitlements)
                    .ThenInclude(entitlement => entitlement.AppPageAction)
                    .ThenInclude(link => link.PermissionAction)
                .Include(subscription => subscription.ChangeLogs
                    .OrderByDescending(change => change.OccurredAtUtc)
                    .Take(50));

        private static CompanySubscriptionDto Map(CompanySubscription subscription)
            => new(
                subscription.Id,
                subscription.CompanyId,
                subscription.SubscriptionPlanId,
                subscription.SubscriptionPlan.Code,
                subscription.SubscriptionPlan.Name,
                subscription.StartsAtUtc,
                subscription.EndsAtUtc,
                subscription.Status,
                subscription.PriceSnapshot,
                subscription.CurrencySnapshot,
                subscription.DurationInDaysSnapshot,
                subscription.Entitlements.Any(entitlement => entitlement.IsCustomized),
                Convert.ToBase64String(subscription.RowVersion),
                subscription.Entitlements
                    .Select(entitlement => new PermissionSelectionDto(
                        entitlement.AppPageAction.AppPage.Key,
                        entitlement.AppPageAction.PermissionAction.Key))
                    .OrderBy(permission => permission.PageKey)
                    .ThenBy(permission => permission.ActionKey)
                    .ToList(),
                subscription.ChangeLogs
                    .OrderByDescending(change => change.OccurredAtUtc)
                    .Select(change => new SubscriptionChangeLogDto(
                        change.Id,
                        change.ChangeType,
                        change.PreviousStatus,
                        change.NewStatus,
                        change.Details,
                        change.OccurredAtUtc,
                        change.PerformedByUserId))
                    .ToList());

        private async Task<EntitlementResolution> ResolveEntitlementsAsync(
            SubscriptionPlan plan,
            IReadOnlyList<PermissionSelectionDto>? requested,
            CancellationToken cancellationToken)
        {
            var planPermissions = plan.Permissions
                .Select(permission => new ResolvedPermission(
                    permission.AppPageId,
                    permission.PermissionActionId,
                    string.Empty,
                    string.Empty))
                .ToList();

            if (requested is null)
                return EntitlementResolution.Valid(planPermissions, false);

            var resolution = await PermissionSelectionResolver.ResolveAsync(
                context,
                requested,
                requireAtLeastOne: true,
                cancellationToken);
            if (!resolution.Success)
                return EntitlementResolution.Invalid(resolution.Error ?? "Invalid entitlements.");

            var planKeys = planPermissions
                .Select(permission => (permission.AppPageId, permission.PermissionActionId))
                .ToHashSet();
            var requestedKeys = resolution.Permissions
                .Select(permission => (permission.AppPageId, permission.PermissionActionId))
                .ToHashSet();

            return EntitlementResolution.Valid(
                resolution.Permissions,
                !planKeys.SetEquals(requestedKeys));
        }

        private void ApplyEntitlements(
            CompanySubscription subscription,
            IReadOnlyList<ResolvedPermission> permissions,
            bool isCustomized,
            DateTime now)
        {
            var desiredKeys = permissions
                .Select(permission => (permission.AppPageId, permission.PermissionActionId))
                .ToHashSet();
            var existingKeys = subscription.Entitlements
                .Select(entitlement => (entitlement.AppPageId, entitlement.PermissionActionId))
                .ToHashSet();

            context.CompanyEntitlements.RemoveRange(subscription.Entitlements.Where(entitlement =>
                !desiredKeys.Contains((entitlement.AppPageId, entitlement.PermissionActionId))));

            foreach (var entitlement in subscription.Entitlements)
                entitlement.IsCustomized = isCustomized;

            foreach (var permission in permissions.Where(permission =>
                !existingKeys.Contains((permission.AppPageId, permission.PermissionActionId))))
            {
                subscription.Entitlements.Add(new CompanyEntitlement
                {
                    CompanyId = subscription.CompanyId,
                    AppPageId = permission.AppPageId,
                    PermissionActionId = permission.PermissionActionId,
                    IsCustomized = isCustomized,
                    CreatedAtUtc = now
                });
            }
        }

        private static SubscriptionChangeLog CreateLog(
            CompanySubscription subscription,
            SubscriptionChangeType changeType,
            SubscriptionStatus? previousStatus,
            SubscriptionStatus newStatus,
            string details,
            string? performedByUserId,
            DateTime occurredAtUtc)
            => new()
            {
                CompanyId = subscription.CompanyId,
                ChangeType = changeType,
                PreviousStatus = previousStatus,
                NewStatus = newStatus,
                Details = details,
                PerformedByUserId = performedByUserId,
                OccurredAtUtc = occurredAtUtc
            };

        private static DateTime NormalizeUtc(DateTime value)
            => value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };

        private sealed record EntitlementResolution(
            bool Success,
            IReadOnlyList<ResolvedPermission> Permissions,
            bool IsCustomized,
            string? Error)
        {
            public static EntitlementResolution Valid(
                IReadOnlyList<ResolvedPermission> permissions,
                bool isCustomized)
                => new(true, permissions, isCustomized, null);

            public static EntitlementResolution Invalid(string error)
                => new(false, [], false, error);
        }
    }
}
