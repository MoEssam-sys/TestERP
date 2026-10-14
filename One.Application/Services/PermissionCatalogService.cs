using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using One.Domain.Base;
using One.Domain.DTOs.Identity;
using One.Infrastructure.DbContext.UsersDbContext;
using One.Infrastructure.Interfaces;

namespace One.Application.Services
{
    public sealed class PermissionCatalogService(
        UsersDbContext context,
        ILogger<PermissionCatalogService> logger) : IPermissionCatalogService
    {
        public async Task<Result<IReadOnlyList<PermissionCatalogModuleDto>>> GetAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var modules = await context.AppModules
                    .AsNoTracking()
                    .AsSplitQuery()
                    .Where(module => module.IsActive)
                    .OrderBy(module => module.SortOrder)
                    .ThenBy(module => module.Key)
                    .Select(module => new PermissionCatalogModuleDto(
                        module.Key,
                        module.NameResourceKey,
                        module.SortOrder,
                        module.IsAssignableToPlans,
                        module.Pages
                            .Where(page => page.IsActive)
                            .OrderBy(page => page.SortOrder)
                            .ThenBy(page => page.Key)
                            .Select(page => new PermissionCatalogPageDto(
                                page.Key,
                                page.NameResourceKey,
                                page.Route,
                                page.SortOrder,
                                page.IsNavigationVisible,
                                page.Actions
                                    .Where(link => link.PermissionAction.IsActive)
                                    .OrderBy(link => link.PermissionAction.SortOrder)
                                    .ThenBy(link => link.PermissionAction.Key)
                                    .Select(link => new PermissionCatalogActionDto(
                                        link.PermissionAction.Key,
                                        link.PermissionAction.NameResourceKey,
                                        link.PermissionAction.SortOrder))
                                    .ToList()))
                            .ToList()))
                    .ToListAsync(cancellationToken);

                return Result<IReadOnlyList<PermissionCatalogModuleDto>>.Ok(modules);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to read the permission catalog.");
                return Result<IReadOnlyList<PermissionCatalogModuleDto>>.Fail(
                    ["Unable to load the permission catalog."],
                    500);
            }
        }
    }
}
