using Microsoft.EntityFrameworkCore;
using One.Domain.Entities.Identity;
using One.Infrastructure.DbContext.UsersDbContext;

namespace One.Application.Services.Subscriptions
{
    internal static class PermissionVersionTracker
    {
        public static async Task StageBumpAsync(
            UsersDbContext context,
            int companyId,
            CancellationToken cancellationToken)
        {
            var version = await context.CompanyPermissionVersions.FindAsync([companyId], cancellationToken);
            if (version is null)
            {
                context.CompanyPermissionVersions.Add(new CompanyPermissionVersion
                {
                    CompanyId = companyId,
                    Version = 1,
                    UpdatedAtUtc = DateTime.UtcNow
                });
                return;
            }

            version.Version++;
            version.UpdatedAtUtc = DateTime.UtcNow;
        }

        public static async Task StageBumpsAsync(
            UsersDbContext context,
            IEnumerable<int> companyIds,
            CancellationToken cancellationToken)
        {
            var ids = companyIds.Distinct().ToArray();
            if (ids.Length == 0)
                return;

            var versions = await context.CompanyPermissionVersions
                .Where(version => ids.Contains(version.CompanyId))
                .ToDictionaryAsync(version => version.CompanyId, cancellationToken);
            var now = DateTime.UtcNow;
            foreach (var companyId in ids)
            {
                if (versions.TryGetValue(companyId, out var version))
                {
                    version.Version++;
                    version.UpdatedAtUtc = now;
                }
                else
                {
                    context.CompanyPermissionVersions.Add(new CompanyPermissionVersion
                    {
                        CompanyId = companyId,
                        Version = 1,
                        UpdatedAtUtc = now
                    });
                }
            }
        }
    }
}
