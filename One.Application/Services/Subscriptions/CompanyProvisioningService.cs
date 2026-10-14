using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
    public sealed class CompanyProvisioningService(
        UsersDbContext context,
        UserManager<UserData> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<CompanyProvisioningService> logger) : ICompanyProvisioningService
    {
        public async Task<Result<ProvisionCompanyResultDto>> CreateAsync(
            ProvisionCompanyRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            var name = request.Name.Trim();
            var code = request.Code.Trim().ToUpperInvariant();
            var userName = request.Admin.UserName.Trim();
            var email = string.IsNullOrWhiteSpace(request.Admin.Email)
                ? null
                : request.Admin.Email.Trim();

            if (name.Length == 0 || code.Length == 0 || userName.Length == 0)
                return Result<ProvisionCompanyResultDto>.Fail(["Company and administrator details are required."]);

            if (await context.Companies.AsNoTracking().AnyAsync(
                company => company.Code == code,
                cancellationToken))
            {
                return Result<ProvisionCompanyResultDto>.Fail(["Company code already exists."], 409);
            }

            if (await userManager.FindByNameAsync(userName) is not null)
                return Result<ProvisionCompanyResultDto>.Fail(["Username already exists."], 409);

            if (email is not null && await userManager.FindByEmailAsync(email) is not null)
                return Result<ProvisionCompanyResultDto>.Fail(["Email already exists."], 409);

            if (!await roleManager.RoleExistsAsync(SystemRoles.CompanyAdmin))
                return Result<ProvisionCompanyResultDto>.Fail(["Company administrator role is not configured."], 500);

            var plan = await context.SubscriptionPlans
                .AsNoTracking()
                .Include(item => item.Permissions)
                .FirstOrDefaultAsync(item => item.Id == request.SubscriptionPlanId, cancellationToken);
            if (plan is null || !plan.IsActive)
                return Result<ProvisionCompanyResultDto>.Fail(["Subscription plan was not found or is inactive."], 404);

            var permissionResolution = await ResolvePermissionsAsync(
                plan,
                request.Entitlements,
                cancellationToken);
            if (!permissionResolution.Success)
                return Result<ProvisionCompanyResultDto>.Fail([permissionResolution.Error]);

            var now = DateTime.UtcNow;
            var startsAtUtc = NormalizeUtc(request.StartsAtUtc ?? now);
            if (startsAtUtc > now.AddYears(1))
                return Result<ProvisionCompanyResultDto>.Fail(["Subscription start cannot be more than one year in the future."]);

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var company = new Company
                {
                    Name = name,
                    Code = code,
                    IsActive = true,
                    CreatedOn = now
                };
                context.Companies.Add(company);
                await context.SaveChangesAsync(cancellationToken);

                var endsAtUtc = startsAtUtc.AddDays(plan.DurationInDays);
                var status = endsAtUtc <= now
                    ? SubscriptionStatus.Expired
                    : startsAtUtc > now
                        ? SubscriptionStatus.Pending
                        : SubscriptionStatus.Active;
                var subscription = new CompanySubscription
                {
                    CompanyId = company.Id,
                    SubscriptionPlanId = plan.Id,
                    StartsAtUtc = startsAtUtc,
                    EndsAtUtc = endsAtUtc,
                    Status = status,
                    PriceSnapshot = plan.Price,
                    CurrencySnapshot = plan.Currency,
                    DurationInDaysSnapshot = plan.DurationInDays,
                    CreatedAtUtc = now,
                    CreatedByUserId = currentUserId.ToString()
                };

                foreach (var permission in permissionResolution.Permissions)
                {
                    subscription.Entitlements.Add(new CompanyEntitlement
                    {
                        CompanyId = company.Id,
                        AppPageId = permission.AppPageId,
                        PermissionActionId = permission.PermissionActionId,
                        IsCustomized = permissionResolution.IsCustomized,
                        CreatedAtUtc = now
                    });
                }

                subscription.ChangeLogs.Add(new SubscriptionChangeLog
                {
                    CompanyId = company.Id,
                    ChangeType = SubscriptionChangeType.Created,
                    PreviousStatus = null,
                    NewStatus = status,
                    Details = $"Created with company from plan '{plan.Code}'.",
                    PerformedByUserId = currentUserId.ToString(),
                    OccurredAtUtc = now
                });
                context.CompanySubscriptions.Add(subscription);
                await PermissionVersionTracker.StageBumpAsync(context, company.Id, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);

                var admin = new UserData
                {
                    LockoutEnabled = true,
                    UserName = userName,
                    Email = email,
                    FirstName = request.Admin.FirstName.Trim(),
                    LastName = request.Admin.LastName.Trim(),
                    PhoneNumber = request.Admin.PhoneNumber?.Trim(),
                    CompanyId = company.Id,
                    IsActive = true,
                    SecurityStamp = Guid.NewGuid().ToString()
                };

                var createUser = await userManager.CreateAsync(admin, request.Admin.Password);
                if (!createUser.Succeeded)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return IdentityFailure(createUser);
                }

                var addRole = await userManager.AddToRoleAsync(admin, SystemRoles.CompanyAdmin);
                if (!addRole.Succeeded)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return IdentityFailure(addRole);
                }

                await transaction.CommitAsync(cancellationToken);
                return Result<ProvisionCompanyResultDto>.Ok(new ProvisionCompanyResultDto(
                    company.Id,
                    subscription.Id,
                    admin.Id,
                    startsAtUtc,
                    endsAtUtc,
                    status,
                    permissionResolution.Permissions.Count));
            }
            catch (DbUpdateException exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                logger.LogWarning(exception, "Company provisioning conflicted for code {CompanyCode}.", code);
                return Result<ProvisionCompanyResultDto>.Fail(
                    ["Company, user, or subscription data conflicts with an existing record."],
                    409);
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                logger.LogError(exception, "Failed to provision company {CompanyCode}.", code);
                return Result<ProvisionCompanyResultDto>.Fail(["Unable to create the company."], 500);
            }
        }

        private async Task<ProvisioningPermissionResolution> ResolvePermissionsAsync(
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
                return ProvisioningPermissionResolution.Valid(planPermissions, false);

            var resolution = await PermissionSelectionResolver.ResolveAsync(
                context,
                requested,
                requireAtLeastOne: true,
                cancellationToken);
            if (!resolution.Success)
                return ProvisioningPermissionResolution.Invalid(resolution.Error ?? "Invalid entitlements.");

            var planKeys = planPermissions
                .Select(permission => (permission.AppPageId, permission.PermissionActionId))
                .ToHashSet();
            var requestedKeys = resolution.Permissions
                .Select(permission => (permission.AppPageId, permission.PermissionActionId))
                .ToHashSet();
            return ProvisioningPermissionResolution.Valid(
                resolution.Permissions,
                !planKeys.SetEquals(requestedKeys));
        }

        private static Result<ProvisionCompanyResultDto> IdentityFailure(IdentityResult result)
            => Result<ProvisionCompanyResultDto>.Fail(
                result.Errors.Select(error => (string?)error.Description).ToList());

        private static DateTime NormalizeUtc(DateTime value)
            => value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };

        private sealed record ProvisioningPermissionResolution(
            bool Success,
            IReadOnlyList<ResolvedPermission> Permissions,
            bool IsCustomized,
            string? Error)
        {
            public static ProvisioningPermissionResolution Valid(
                IReadOnlyList<ResolvedPermission> permissions,
                bool isCustomized)
                => new(true, permissions, isCustomized, null);

            public static ProvisioningPermissionResolution Invalid(string error)
                => new(false, [], false, error);
        }
    }
}
