using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using One.Domain.Base;
using One.Domain.Constants;
using One.Domain.DTOs.Identity;
using One.Infrastructure.Interfaces;

namespace One.API.Controllers
{
    [Route("api/subscription-plans")]
    [Authorize(Policy = AuthorizationPolicies.ManageCompanies)]
    public sealed class SubscriptionPlansController(
        ISubscriptionPlanService subscriptionPlanService) : BaseApiController
    {
        [HttpGet]
        public async Task<ActionResult<Result<IReadOnlyList<SubscriptionPlanDto>>>> GetAll(
            CancellationToken cancellationToken)
            => ToActionResult(await subscriptionPlanService.GetAllAsync(cancellationToken));

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Result<SubscriptionPlanDto>>> GetById(
            int id,
            CancellationToken cancellationToken)
            => ToActionResult(await subscriptionPlanService.GetByIdAsync(id, cancellationToken));

        [HttpPost]
        public async Task<ActionResult<Result<SubscriptionPlanDto>>> Create(
            CreateSubscriptionPlanRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue)
                return Unauthorized(Result<SubscriptionPlanDto>.Fail(["Authenticated user identifier is missing."], 401));

            var result = await subscriptionPlanService.CreateAsync(request, userId.Value, cancellationToken);
            return ToActionResult(result, result.Success ? StatusCodes.Status201Created : null);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Result<SubscriptionPlanDto>>> Update(
            int id,
            UpdateSubscriptionPlanRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue)
                return Unauthorized(Result<SubscriptionPlanDto>.Fail(["Authenticated user identifier is missing."], 401));

            return ToActionResult(await subscriptionPlanService.UpdateAsync(
                id,
                request,
                userId.Value,
                cancellationToken));
        }

        [HttpPost("{id:int}/active")]
        public async Task<ActionResult<Result<bool>>> SetActive(
            int id,
            SetSubscriptionPlanActiveRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue)
                return Unauthorized(Result<bool>.Fail(["Authenticated user identifier is missing."], 401));

            return ToActionResult(await subscriptionPlanService.SetActiveAsync(
                id,
                request,
                userId.Value,
                cancellationToken));
        }
    }
}
