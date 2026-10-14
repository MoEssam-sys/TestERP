using Microsoft.Extensions.Logging;
using One.Application.Base;
using One.Domain.Entities.Sales;
using One.Infrastructure.DbContext.MainDbContext;
using One.Domain.Tenancy;

namespace One.Application.Repos.Sales
{
    public class BrandsRepo : BaseRepository<Brands, Guid>
    {
        protected override IReadOnlySet<string> AllowedQueryColumns { get; } =
            new HashSet<string>(["Id", "Code", "Name", "IsActive", "CreatedAt"], StringComparer.OrdinalIgnoreCase);

        public BrandsRepo(
            IDapperConnectionFactory dbFactory,
            ITenantContext tenantContext,
            ILogger<BrandsRepo> logger)
            : base(dbFactory, tenantContext, logger)
        { }
    }
}
