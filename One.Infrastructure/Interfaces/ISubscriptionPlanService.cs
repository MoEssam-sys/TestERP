using One.Domain.Base;
using One.Domain.DTOs.Identity;

namespace One.Infrastructure.Interfaces
{
    public interface ISubscriptionPlanService
    {
        Task<Result<IReadOnlyList<SubscriptionPlanDto>>> GetAllAsync(CancellationToken cancellationToken = default);

        Task<Result<SubscriptionPlanDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        Task<Result<SubscriptionPlanDto>> CreateAsync(
            CreateSubscriptionPlanRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default);

        Task<Result<SubscriptionPlanDto>> UpdateAsync(
            int id,
            UpdateSubscriptionPlanRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default);

        Task<Result<bool>> SetActiveAsync(
            int id,
            SetSubscriptionPlanActiveRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default);
    }
}
