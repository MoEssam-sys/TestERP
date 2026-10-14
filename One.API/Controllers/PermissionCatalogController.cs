using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using One.Domain.Base;
using One.Domain.Constants;
using One.Domain.DTOs.Identity;
using One.Infrastructure.Interfaces;

namespace One.API.Controllers
{
    [Route("api/permission-catalog")]
    [Authorize(Policy = AuthorizationPolicies.ManageCompanies)]
    public sealed class PermissionCatalogController(
        IPermissionCatalogService permissionCatalogService) : BaseApiController
    {
        [HttpGet]
        public async Task<ActionResult<Result<IReadOnlyList<PermissionCatalogModuleDto>>>> Get(
            CancellationToken cancellationToken)
        {
            var result = await permissionCatalogService.GetAsync(cancellationToken);
            return ToActionResult(result);
        }
    }
}
