using Microsoft.Extensions.Logging;
using One.Application.Base;
using One.Domain.Entities.Purchases;
using One.Infrastructure.DbContext.MainDbContext;
using One.Domain.Tenancy;

namespace One.Application.Repos.Purchases
{
    public class SuppliersRepo : BaseRepository<Suppliers, Guid>
    {
        protected override IReadOnlySet<string> AllowedQueryColumns { get; } =
            new HashSet<string>(["Id", "Code", "Name", "Address", "Email", "Phone", "IsActive", "CreatedAt"], StringComparer.OrdinalIgnoreCase);

        public SuppliersRepo(
            IDapperConnectionFactory dbFactory,
            ITenantContext tenantContext,
            ILogger<SuppliersRepo> logger)
            : base(dbFactory, tenantContext, logger)
        { }
    }
}
