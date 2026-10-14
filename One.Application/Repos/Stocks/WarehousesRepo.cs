using Microsoft.Extensions.Logging;
using One.Application.Base;
using One.Domain.Entities.Stocks;
using One.Infrastructure.DbContext.MainDbContext;
using One.Domain.Tenancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace One.Application.Repos.Stocks
{

    public class WarehousesRepo : BaseRepository<Warehouses, Guid>
    {
        protected override IReadOnlySet<string> AllowedQueryColumns { get; } =
            new HashSet<string>(["Id", "BranchId", "Code", "Name", "IsActive", "CreatedAt"], StringComparer.OrdinalIgnoreCase);

        public WarehousesRepo(
            IDapperConnectionFactory dbFactory,
            ITenantContext tenantContext,
            ILogger<WarehousesRepo> logger)
            : base(dbFactory, tenantContext, logger)
        { }
    }
}
