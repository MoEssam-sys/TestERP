using One.Domain.Base;
using One.Domain.DTOs.Identity;

namespace One.Infrastructure.Interfaces
{
    public interface ICompanyProvisioningService
    {
        Task<Result<ProvisionCompanyResultDto>> CreateAsync(
            ProvisionCompanyRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default);
    }
}
