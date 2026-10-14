using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using One.Domain.Base;
using One.Domain.Constants;
using One.Domain.DTOs.Identity;
using One.Infrastructure.Interfaces;

namespace One.API.Controllers
{
    [Route("api/company-subscriptions")]
    [Authorize(Policy = AuthorizationPolicies.ManageCompanies)]
    public sealed class CompanySubscriptionsController(
        ICompanySubscriptionService companySubscriptionService) : BaseApiController
    {
        [HttpGet("company/{companyId:int}")]
        public async Task<ActionResult<Result<IReadOnlyList<CompanySubscriptionDto>>>> GetByCompany(
            int companyId,
            CancellationToken cancellationToken)
            => ToActionResult(await companySubscriptionService.GetByCompanyAsync(
                companyId,
                cancellationToken));

        [HttpPost]
        public async Task<ActionResult<Result<CompanySubscriptionDto>>> Create(
            CreateCompanySubscriptionRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue)
                return Unauthorized(Result<CompanySubscriptionDto>.Fail(["Authenticated user identifier is missing."], 401));

            var result = await companySubscriptionService.CreateAsync(request, userId.Value, cancellationToken);
            return ToActionResult(result, result.Success ? StatusCodes.Status201Created : null);
        }

        [HttpPost("{id:long}/renew")]
        public async Task<ActionResult<Result<CompanySubscriptionDto>>> Renew(
            long id,
            RenewCompanySubscriptionRequest request,
            CancellationToken cancellationToken)
            => ToActionResult(await WithUserAsync(userId => companySubscriptionService.RenewAsync(
                id,
                request,
                userId,
                cancellationToken)));

        [HttpPut("{id:long}/entitlements")]
        public async Task<ActionResult<Result<CompanySubscriptionDto>>> ReplaceEntitlements(
            long id,
            ReplaceCompanyEntitlementsRequest request,
            CancellationToken cancellationToken)
            => ToActionResult(await WithUserAsync(userId => companySubscriptionService.ReplaceEntitlementsAsync(
                id,
                request,
                userId,
                cancellationToken)));

        [HttpPost("{id:long}/suspend")]
        public async Task<ActionResult<Result<CompanySubscriptionDto>>> Suspend(
            long id,
            ChangeSubscriptionStatusRequest request,
            CancellationToken cancellationToken)
            => ToActionResult(await WithUserAsync(userId => companySubscriptionService.SuspendAsync(
                id,
                request,
                userId,
                cancellationToken)));

        [HttpPost("{id:long}/resume")]
        public async Task<ActionResult<Result<CompanySubscriptionDto>>> Resume(
            long id,
            ChangeSubscriptionStatusRequest request,
            CancellationToken cancellationToken)
            => ToActionResult(await WithUserAsync(userId => companySubscriptionService.ResumeAsync(
                id,
                request,
                userId,
                cancellationToken)));

        [HttpPost("{id:long}/cancel")]
        public async Task<ActionResult<Result<CompanySubscriptionDto>>> Cancel(
            long id,
            ChangeSubscriptionStatusRequest request,
            CancellationToken cancellationToken)
            => ToActionResult(await WithUserAsync(userId => companySubscriptionService.CancelAsync(
                id,
                request,
                userId,
                cancellationToken)));

        private async Task<Result<CompanySubscriptionDto>> WithUserAsync(
            Func<Guid, Task<Result<CompanySubscriptionDto>>> operation)
        {
            var userId = GetAuthenticatedUserId();
            return userId.HasValue
                ? await operation(userId.Value)
                : Result<CompanySubscriptionDto>.Fail(["Authenticated user identifier is missing."], 401);
        }
    }
}
