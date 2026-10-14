using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using One.Domain.Base;
using One.Domain.DTOs.Identity;
using One.Domain.Entities.Identity;
using One.Infrastructure.DbContext.UsersDbContext;
using One.Infrastructure.Interfaces;

namespace One.Application.Services.Subscriptions
{
    public sealed class SubscriptionPlanService(
        UsersDbContext context,
        ILogger<SubscriptionPlanService> logger) : ISubscriptionPlanService
    {
        public async Task<Result<IReadOnlyList<SubscriptionPlanDto>>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var plans = await PlanQuery()
                    .OrderByDescending(plan => plan.IsActive)
                    .ThenBy(plan => plan.Name)
                    .ToListAsync(cancellationToken);

                return Result<IReadOnlyList<SubscriptionPlanDto>>.Ok(plans.Select(Map).ToList());
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to read subscription plans.");
                return Result<IReadOnlyList<SubscriptionPlanDto>>.Fail(
                    ["Unable to load subscription plans."],
                    500);
            }
        }

        public async Task<Result<SubscriptionPlanDto>> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var plan = await PlanQuery()
                    .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

                return plan is null
                    ? Result<SubscriptionPlanDto>.Fail(["Subscription plan was not found."], 404)
                    : Result<SubscriptionPlanDto>.Ok(Map(plan));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to read subscription plan {PlanId}.", id);
                return Result<SubscriptionPlanDto>.Fail(["Unable to load the subscription plan."], 500);
            }
        }

        public async Task<Result<SubscriptionPlanDto>> CreateAsync(
            CreateSubscriptionPlanRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            var validationError = ValidatePlan(
                request.Code,
                request.Name,
                request.Price,
                request.Currency,
                request.DurationInDays);
            if (validationError is not null)
                return Result<SubscriptionPlanDto>.Fail([validationError]);

            var resolution = await PermissionSelectionResolver.ResolveAsync(
                context,
                request.Permissions,
                requireAtLeastOne: true,
                cancellationToken);
            if (!resolution.Success)
                return Result<SubscriptionPlanDto>.Fail([resolution.Error]);

            var normalizedCode = request.Code.Trim().ToUpperInvariant();
            if (await context.SubscriptionPlans.AnyAsync(
                plan => plan.Code == normalizedCode,
                cancellationToken))
            {
                return Result<SubscriptionPlanDto>.Fail(["Subscription plan code already exists."], 409);
            }

            var plan = new SubscriptionPlan
            {
                Code = normalizedCode,
                Name = request.Name.Trim(),
                Description = NormalizeOptional(request.Description),
                Price = request.Price,
                Currency = request.Currency.Trim().ToUpperInvariant(),
                DurationInDays = request.DurationInDays,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = currentUserId.ToString(),
                Permissions = resolution.Permissions.Select(permission => new PlanPermission
                {
                    AppPageId = permission.AppPageId,
                    PermissionActionId = permission.PermissionActionId
                }).ToList()
            };

            try
            {
                context.SubscriptionPlans.Add(plan);
                await context.SaveChangesAsync(cancellationToken);
                context.ChangeTracker.Clear();
                return await GetByIdAsync(plan.Id, cancellationToken);
            }
            catch (DbUpdateException exception)
            {
                logger.LogWarning(exception, "Subscription plan creation conflicted for code {PlanCode}.", normalizedCode);
                return Result<SubscriptionPlanDto>.Fail(["Subscription plan could not be created because its data conflicts with an existing plan."], 409);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to create subscription plan.");
                return Result<SubscriptionPlanDto>.Fail(["Unable to create the subscription plan."], 500);
            }
        }

        public async Task<Result<SubscriptionPlanDto>> UpdateAsync(
            int id,
            UpdateSubscriptionPlanRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            var validationError = ValidatePlan(
                request.Code,
                request.Name,
                request.Price,
                request.Currency,
                request.DurationInDays);
            if (validationError is not null)
                return Result<SubscriptionPlanDto>.Fail([validationError]);

            if (!TryDecodeRowVersion(request.RowVersion, out var rowVersion))
                return Result<SubscriptionPlanDto>.Fail(["Invalid plan version."]);

            var resolution = await PermissionSelectionResolver.ResolveAsync(
                context,
                request.Permissions,
                requireAtLeastOne: true,
                cancellationToken);
            if (!resolution.Success)
                return Result<SubscriptionPlanDto>.Fail([resolution.Error]);

            var plan = await context.SubscriptionPlans
                .Include(item => item.Permissions)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (plan is null)
                return Result<SubscriptionPlanDto>.Fail(["Subscription plan was not found."], 404);

            var normalizedCode = request.Code.Trim().ToUpperInvariant();
            if (await context.SubscriptionPlans.AnyAsync(
                item => item.Id != id && item.Code == normalizedCode,
                cancellationToken))
            {
                return Result<SubscriptionPlanDto>.Fail(["Subscription plan code already exists."], 409);
            }

            context.Entry(plan).Property(item => item.RowVersion).OriginalValue = rowVersion;
            plan.Code = normalizedCode;
            plan.Name = request.Name.Trim();
            plan.Description = NormalizeOptional(request.Description);
            plan.Price = request.Price;
            plan.Currency = request.Currency.Trim().ToUpperInvariant();
            plan.DurationInDays = request.DurationInDays;
            plan.UpdatedAtUtc = DateTime.UtcNow;
            plan.UpdatedByUserId = currentUserId.ToString();

            var desiredPermissionKeys = resolution.Permissions
                .Select(permission => (permission.AppPageId, permission.PermissionActionId))
                .ToHashSet();
            var existingPermissionKeys = plan.Permissions
                .Select(permission => (permission.AppPageId, permission.PermissionActionId))
                .ToHashSet();

            context.PlanPermissions.RemoveRange(plan.Permissions.Where(permission =>
                !desiredPermissionKeys.Contains((permission.AppPageId, permission.PermissionActionId))));

            foreach (var permission in resolution.Permissions.Where(permission =>
                !existingPermissionKeys.Contains((permission.AppPageId, permission.PermissionActionId))))
            {
                plan.Permissions.Add(new PlanPermission
                {
                    SubscriptionPlanId = plan.Id,
                    AppPageId = permission.AppPageId,
                    PermissionActionId = permission.PermissionActionId
                });
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                context.ChangeTracker.Clear();
                return await GetByIdAsync(id, cancellationToken);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                logger.LogInformation(exception, "Subscription plan {PlanId} has a concurrency conflict.", id);
                return Result<SubscriptionPlanDto>.Fail(["The subscription plan was changed by another user. Reload and try again."], 409);
            }
            catch (DbUpdateException exception)
            {
                logger.LogWarning(exception, "Subscription plan update conflicted for {PlanId}.", id);
                return Result<SubscriptionPlanDto>.Fail(["Subscription plan could not be updated because its data conflicts with another plan."], 409);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to update subscription plan {PlanId}.", id);
                return Result<SubscriptionPlanDto>.Fail(["Unable to update the subscription plan."], 500);
            }
        }

        public async Task<Result<bool>> SetActiveAsync(
            int id,
            SetSubscriptionPlanActiveRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            if (!TryDecodeRowVersion(request.RowVersion, out var rowVersion))
                return Result<bool>.Fail(["Invalid plan version."]);

            var plan = await context.SubscriptionPlans.FirstOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);
            if (plan is null)
                return Result<bool>.Fail(["Subscription plan was not found."], 404);

            context.Entry(plan).Property(item => item.RowVersion).OriginalValue = rowVersion;
            plan.IsActive = request.IsActive;
            plan.UpdatedAtUtc = DateTime.UtcNow;
            plan.UpdatedByUserId = currentUserId.ToString();

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return Result<bool>.Ok(true);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                logger.LogInformation(exception, "Subscription plan {PlanId} has a concurrency conflict.", id);
                return Result<bool>.Fail(["The subscription plan was changed by another user. Reload and try again."], 409);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to change subscription plan {PlanId} status.", id);
                return Result<bool>.Fail(["Unable to change the subscription plan status."], 500);
            }
        }

        private IQueryable<SubscriptionPlan> PlanQuery()
            => context.SubscriptionPlans
                .AsNoTracking()
                .AsSplitQuery()
                .Include(plan => plan.Permissions)
                    .ThenInclude(permission => permission.AppPageAction)
                    .ThenInclude(link => link.AppPage)
                .Include(plan => plan.Permissions)
                    .ThenInclude(permission => permission.AppPageAction)
                    .ThenInclude(link => link.PermissionAction);

        private static SubscriptionPlanDto Map(SubscriptionPlan plan)
            => new(
                plan.Id,
                plan.Code,
                plan.Name,
                plan.Description,
                plan.Price,
                plan.Currency,
                plan.DurationInDays,
                plan.IsActive,
                Convert.ToBase64String(plan.RowVersion),
                plan.Permissions
                    .Select(permission => new PermissionSelectionDto(
                        permission.AppPageAction.AppPage.Key,
                        permission.AppPageAction.PermissionAction.Key))
                    .OrderBy(permission => permission.PageKey)
                    .ThenBy(permission => permission.ActionKey)
                    .ToList());

        private static string? ValidatePlan(
            string? code,
            string? name,
            decimal price,
            string? currency,
            int durationInDays)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 50)
                return "Plan code is required and cannot exceed 50 characters.";
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150)
                return "Plan name is required and cannot exceed 150 characters.";
            if (price < 0)
                return "Plan price cannot be negative.";
            if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
                return "Currency must be a three-letter code.";
            if (durationInDays is < 1 or > 3650)
                return "Plan duration must be between 1 and 3650 days.";
            return null;
        }

        private static string? NormalizeOptional(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        internal static bool TryDecodeRowVersion(string? value, out byte[] rowVersion)
        {
            try
            {
                rowVersion = Convert.FromBase64String(value ?? string.Empty);
                return rowVersion.Length > 0;
            }
            catch (FormatException)
            {
                rowVersion = [];
                return false;
            }
        }
    }
}
