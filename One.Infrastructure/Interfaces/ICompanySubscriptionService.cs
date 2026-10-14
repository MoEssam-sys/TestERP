using One.Domain.Base;
using One.Domain.DTOs.Identity;

namespace One.Infrastructure.Interfaces
{
    public interface ICompanySubscriptionService
    {
        Task<Result<IReadOnlyList<CompanySubscriptionDto>>> GetByCompanyAsync(
            int companyId,
            CancellationToken cancellationToken = default);

        Task<Result<CompanySubscriptionDto>> CreateAsync(
            CreateCompanySubscriptionRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default);

        Task<Result<CompanySubscriptionDto>> RenewAsync(
            long id,
            RenewCompanySubscriptionRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default);

        Task<Result<CompanySubscriptionDto>> ReplaceEntitlementsAsync(
            long id,
            ReplaceCompanyEntitlementsRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default);

        Task<Result<CompanySubscriptionDto>> SuspendAsync(
            long id,
            ChangeSubscriptionStatusRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default);

        Task<Result<CompanySubscriptionDto>> ResumeAsync(
            long id,
            ChangeSubscriptionStatusRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default);

        Task<Result<CompanySubscriptionDto>> CancelAsync(
            long id,
            ChangeSubscriptionStatusRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default);

        Task<int> RefreshStatusesAsync(CancellationToken cancellationToken = default);
    }
}
