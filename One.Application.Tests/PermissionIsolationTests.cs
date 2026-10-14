using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using One.Application.Services.Subscriptions;
using One.Domain.Constants;
using One.Domain.Entities.Identity;
using One.Domain.Enums;
using One.Infrastructure.DbContext.UsersDbContext;

namespace One.Application.Tests;

public sealed class PermissionIsolationTests
{
    [Fact]
    public async Task GetEffectiveAsync_TwoCompanies_ReturnsOnlyTenantEntitlements()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new PermissionService(
            context,
            cache,
            NullLogger<PermissionService>.Instance);

        var tenantOne = await service.GetEffectiveAsync(
            Principal("tenant-one-user", 101),
            TestContext.Current.CancellationToken);
        var tenantTwo = await service.GetEffectiveAsync(
            Principal("tenant-two-user", 202),
            TestContext.Current.CancellationToken);

        Assert.True(tenantOne.Success);
        Assert.True(tenantTwo.Success);
        Assert.Contains(
            tenantOne.Data!.Permissions,
            permission => permission is { PageKey: "Sales.Brands", ActionKey: "View" });
        Assert.Empty(tenantTwo.Data!.Permissions);
        Assert.Equal(101, tenantOne.Data.CompanyId);
        Assert.Equal(202, tenantTwo.Data.CompanyId);
    }

    private static async Task SeedAsync(UsersDbContext context)
    {
        var now = DateTime.UtcNow;
        var companies = new[]
        {
            new Company { Id = 101, Code = "CT101", Name = "Tenant 101", IsActive = true },
            new Company { Id = 202, Code = "CT202", Name = "Tenant 202", IsActive = true }
        };
        var users = new[]
        {
            User("tenant-one-user", 101),
            User("tenant-two-user", 202)
        };
        var module = new AppModule
        {
            Id = 1001,
            Key = "Sales",
            NameResourceKey = "CatalogModuleSales",
            SortOrder = 1,
            IsActive = true,
            IsAssignableToPlans = true
        };
        var page = new AppPage
        {
            Id = 1001,
            AppModuleId = module.Id,
            Key = "Sales.Brands",
            NameResourceKey = "CatalogPageSalesBrands",
            Route = "/sales/brands",
            SortOrder = 1,
            IsNavigationVisible = true,
            IsActive = true
        };
        var action = new PermissionAction
        {
            Id = 1001,
            Key = "View",
            NameResourceKey = "CatalogPermissionView",
            SortOrder = 1,
            IsActive = true
        };
        var link = new AppPageAction
        {
            AppPageId = page.Id,
            PermissionActionId = action.Id
        };
        var plan = new SubscriptionPlan
        {
            Id = 1001,
            Code = "TEST",
            Name = "Test plan",
            Price = 0,
            Currency = "EGP",
            DurationInDays = 30,
            CreatedAtUtc = now,
            CreatedByUserId = "test-runner",
            RowVersion = [1]
        };
        var subscriptions = new[]
        {
            Subscription(1001, 101, plan.Id, now),
            Subscription(1002, 202, plan.Id, now)
        };

        context.AddRange(companies);
        context.AddRange(users);
        context.AddRange(module, page, action, link, plan);
        context.AddRange(subscriptions);
        context.CompanyEntitlements.Add(new CompanyEntitlement
        {
            CompanySubscriptionId = subscriptions[0].Id,
            CompanyId = companies[0].Id,
            AppPageId = page.Id,
            PermissionActionId = action.Id,
            CreatedAtUtc = now
        });
        context.CompanyPermissionVersions.AddRange(
            new CompanyPermissionVersion { CompanyId = 101, Version = 1, UpdatedAtUtc = now },
            new CompanyPermissionVersion { CompanyId = 202, Version = 1, UpdatedAtUtc = now });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static UserData User(string id, int companyId) => new()
    {
        Id = id,
        UserName = id,
        NormalizedUserName = id.ToUpperInvariant(),
        SecurityStamp = $"security-{id}",
        ConcurrencyStamp = $"concurrency-{id}",
        CompanyId = companyId,
        IsActive = true
    };

    private static CompanySubscription Subscription(long id, int companyId, int planId, DateTime now) => new()
    {
        Id = id,
        CompanyId = companyId,
        SubscriptionPlanId = planId,
        StartsAtUtc = now.AddDays(-1),
        EndsAtUtc = now.AddDays(29),
        Status = SubscriptionStatus.Active,
        PriceSnapshot = 0,
        CurrencySnapshot = "EGP",
        DurationInDaysSnapshot = 30,
        CreatedAtUtc = now,
        CreatedByUserId = "test-runner",
        RowVersion = [1]
    };

    private static ClaimsPrincipal Principal(string userId, int companyId) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("companyId", companyId.ToString()),
            new Claim(ClaimTypes.Role, SystemRoles.CompanyAdmin)
        ],
        "Test",
        ClaimTypes.Name,
        ClaimTypes.Role));

    private static UsersDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase($"OneUsersTests-{Guid.NewGuid():N}")
            .Options;
        return new UsersDbContext(options);
    }
}
