using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using One.API.Authorization;
using One.Domain.Base;
using One.Domain.Constants;
using One.Domain.DTOs.Identity;
using One.Infrastructure.Interfaces;

namespace One.API.Controllers
{
    [Authorize]
    [Route("api/permissions")]
    public sealed class PermissionsController(IPermissionService permissionService) : BaseApiController
    {
        [HttpGet("effective")]
        public async Task<ActionResult<Result<EffectivePermissionsDto>>> GetEffective(
            CancellationToken cancellationToken)
            => ToActionResult(await permissionService.GetEffectiveAsync(User, cancellationToken));

        [HttpGet("users/{userId}")]
        [RequirePermission(PermissionCatalogKeys.Pages.Users, PermissionCatalogKeys.Actions.Update)]
        public async Task<ActionResult<Result<UserPermissionEditorDto>>> GetUserPermissions(
            string userId,
            CancellationToken cancellationToken)
            => ToActionResult(await permissionService.GetUserPermissionsAsync(
                userId,
                User,
                cancellationToken));

        [HttpPut("users/{userId}")]
        [RequirePermission(PermissionCatalogKeys.Pages.Users, PermissionCatalogKeys.Actions.Update)]
        public async Task<ActionResult<Result<UserPermissionEditorDto>>> ReplaceUserPermissions(
            string userId,
            ReplaceUserPermissionsRequest request,
            CancellationToken cancellationToken)
            => ToActionResult(await permissionService.ReplaceUserPermissionsAsync(
                userId,
                request,
                User,
                cancellationToken));
    }
}
