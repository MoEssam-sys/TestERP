using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using One.Domain.Base;
using One.Domain.Constants;
using One.Domain.DTOs;
using One.Domain.DTOs.Identity;
using One.Infrastructure.Interfaces;

namespace One.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CompaniesController(
        IAuthService authService,
        IAuthRepository authRepo,
        ICompanyProvisioningService companyProvisioningService) : BaseApiController
    {
        [HttpGet("Validate/{code}")]
        public async Task<IActionResult> Validate(string code)
        {
            var result = await authService.ValidateCompanyAsync(code);
            if (!result.Success)
                return NotFound(new { message = result.Messages?.FirstOrDefault() });

            return Ok(result);
        }

        [Authorize(Policy = AuthorizationPolicies.ManageCompanies)]
        [HttpGet("List")]
        public async Task<IActionResult> List()
        {
            var list = await authRepo.GetAllCompaniesAsync();
            return Ok(Result<object>.Ok(list));
        }

        [Authorize(Policy = AuthorizationPolicies.ManageCompanies)]
        [HttpGet("paged")]
        public async Task<IActionResult> GetPage(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            CancellationToken cancellationToken = default)
        {
            var page = await authRepo.GetCompanySummaryPageAsync(
                pageNumber,
                pageSize,
                search,
                cancellationToken);
            return Ok(Result<PagedResult<CompanySummaryDto>>.Ok(page));
        }

        [Authorize(Policy = AuthorizationPolicies.ManageCompanies)]
        [HttpGet("lookup")]
        public async Task<IActionResult> Lookup(
            [FromQuery] string? search = null,
            [FromQuery] int take = 30,
            CancellationToken cancellationToken = default)
        {
            var companies = await authRepo.SearchCompaniesAsync(search, take, cancellationToken);
            return Ok(Result<IReadOnlyList<CompanyLookupDto>>.Ok(companies));
        }

        [Authorize(Policy = AuthorizationPolicies.ManageCompanies)]
        [HttpGet("lookup/{id:int}")]
        public async Task<IActionResult> LookupById(int id)
        {
            var company = await authRepo.GetCompanyByIdAsync(id);
            if (company is null)
                return NotFound(Result<CompanyLookupDto>.Fail(["Company not found."], 404));

            return Ok(Result<CompanyLookupDto>.Ok(new CompanyLookupDto
            {
                Id = company.Id,
                Name = company.Name,
                Code = company.Code
            }));
        }

        [Authorize(Policy = AuthorizationPolicies.ManageCompanies)]
        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard(
            [FromQuery] string? search = null,
            [FromQuery] int take = 12,
            CancellationToken cancellationToken = default)
        {
            var dashboard = await authRepo.GetCompanyDashboardAsync(search, take, cancellationToken);
            return Ok(Result<CompanyDashboardDto>.Ok(dashboard));
        }

        [Authorize(Policy = AuthorizationPolicies.ManageCompanies)]
        [HttpPost("Create")]
        public async Task<ActionResult<Result<ProvisionCompanyResultDto>>> Create(
            [FromBody] ProvisionCompanyRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue)
                return Unauthorized(Result<ProvisionCompanyResultDto>.Fail(
                    ["Authenticated user identifier is missing."],
                    401));

            var result = await companyProvisioningService.CreateAsync(
                request,
                userId.Value,
                cancellationToken);
            return ToActionResult(result, result.Success ? StatusCodes.Status201Created : null);
        }
        [Authorize(Policy = AuthorizationPolicies.ManageCompanies)]
        [HttpPost("{id}/toggle-status")]
        public async Task<IActionResult> ToggleStatus(int id, [FromQuery] bool isActive)
        {
            var result = await authService.ToggleCompanyStatusAsync(id, isActive);
            if (!result.Success)
                return BadRequest(new { success = false, message = result.Messages?.FirstOrDefault() });

            return Ok(new { success = true, data = result.Data });
        }
    }
}
