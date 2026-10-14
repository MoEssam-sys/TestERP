using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using One.Application.Repos.Identity;
using One.Domain.Entities.Identity;
using One.Infrastructure.DbContext.UsersDbContext;

namespace One.Application.Tests;

public sealed class CompanyQueryPerformanceTests
{
    [Fact]
    public async Task GetCompanySummaryPageAsync_OversizedRequest_ReturnsBoundedStablePage()
    {
        await using var context = CreateContext();
        context.Companies.AddRange(Enumerable.Range(1, 125).Select(index => new Company
        {
            Id = index,
            Name = $"Restaurant {index:D3}",
            Code = $"R{index:D3}"
        }));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = CreateRepository(context);

        var page = await repository.GetCompanySummaryPageAsync(
            1,
            10_000,
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(125, page.TotalCount);
        Assert.Equal(100, page.PageSize);
        Assert.Equal(100, page.Items.Count);
        Assert.Equal("R001", page.Items[0].Code);
        Assert.Equal("R100", page.Items[^1].Code);
    }

    [Fact]
    public async Task SearchCompaniesAsync_SearchAndOversizedTake_ReturnsAtMostFiftyMatches()
    {
        await using var context = CreateContext();
        context.Companies.AddRange(Enumerable.Range(1, 80).Select(index => new Company
        {
            Id = index,
            Name = $"Cafe {index:D2}",
            Code = $"C{index:D2}"
        }));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = CreateRepository(context);

        var matches = await repository.SearchCompaniesAsync(
            "Cafe",
            1_000,
            TestContext.Current.CancellationToken);

        Assert.Equal(50, matches.Count);
        Assert.Equal("C01", matches[0].Code);
        Assert.Equal("C50", matches[^1].Code);
    }

    private static UsersDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new UsersDbContext(options);
    }

    private static AuthRepository CreateRepository(UsersDbContext context)
    {
        var userManager = new Mock<UserManager<UserData>>(
            Mock.Of<IUserStore<UserData>>(),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
        var roleManager = new Mock<RoleManager<IdentityRole>>(
            Mock.Of<IRoleStore<IdentityRole>>(),
            null!,
            null!,
            null!,
            null!);
        return new AuthRepository(userManager.Object, roleManager.Object, context);
    }
}
