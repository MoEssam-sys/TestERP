using One.Domain.Base;
using One.Domain.DTOs.Identity;

namespace One.Infrastructure.Interfaces
{
    public interface IPermissionCatalogService
    {
        Task<Result<IReadOnlyList<PermissionCatalogModuleDto>>> GetAsync(
            CancellationToken cancellationToken = default);
    }
}
