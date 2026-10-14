using Microsoft.Extensions.Logging;
using One.Application.Base;
using One.Domain.Entities.Sales;
using One.Infrastructure.DbContext.MainDbContext;
using One.Domain.Tenancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace One.Application.Repos.Sales
{
    public class BranchesRepo : BaseRepository<Branches, Guid>
    {
        protected override IReadOnlySet<string> AllowedQueryColumns { get; } =
            new HashSet<string>(["Id", "Code", "Name", "Address", "Phone", "IsActive", "CreatedAt"], StringComparer.OrdinalIgnoreCase);

        public BranchesRepo(
            IDapperConnectionFactory dbFactory,
            ITenantContext tenantContext,
            ILogger<BranchesRepo> logger)
            : base(dbFactory, tenantContext, logger)
        { }
    }
}
