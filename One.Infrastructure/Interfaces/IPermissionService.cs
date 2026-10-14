using System.Security.Claims;
using One.Domain.Base;
using One.Domain.DTOs.Identity;

namespace One.Infrastructure.Interfaces
{
    public interface IPermissionService
    {
        Task<Result<EffectivePermissionsDto>> GetEffectiveAsync(
            ClaimsPrincipal user,
            CancellationToken cancellationToken = default);

        Task<bool> HasPermissionAsync(
            ClaimsPrincipal user,
            string pageKey,
            string actionKey,
            CancellationToken cancellationToken = default);

        Task<Result<UserPermissionEditorDto>> GetUserPermissionsAsync(
            string userId,
            ClaimsPrincipal actor,
            CancellationToken cancellationToken = default);

        Task<Result<UserPermissionEditorDto>> ReplaceUserPermissionsAsync(
            string userId,
            ReplaceUserPermissionsRequest request,
            ClaimsPrincipal actor,
            CancellationToken cancellationToken = default);
    }
}
