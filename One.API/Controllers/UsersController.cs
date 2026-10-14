using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using One.API.Authorization;
using One.Domain.Constants;
using One.Domain.DTOs.Identity;
using One.Infrastructure.Interfaces;
using System.Security.Claims;

namespace One.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UsersController(IAuthService authService) : ControllerBase
    {
        [Authorize(Roles = "SuperAdmin")]
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            var result = await authService.GetAllUsersAsync();
            if (!result.Success)
                return BadRequest(new { success = false, message = result.Messages?.FirstOrDefault() });

            return Ok(new { success = true, data = result.Data });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("paged")]
        public async Task<IActionResult> GetUsersPage(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] int? companyId = null,
            CancellationToken cancellationToken = default)
        {
            var result = await authService.GetUserManagementPageAsync(
                companyId,
                pageNumber,
                pageSize,
                search,
                cancellationToken);
            return result.Success
                ? Ok(new { success = true, data = result.Data })
                : StatusCode(result.StatusCode ?? StatusCodes.Status400BadRequest, result);
        }

        [RequirePermission(PermissionCatalogKeys.Pages.Users, PermissionCatalogKeys.Actions.View)]
        [HttpGet("my-company")]
        public async Task<IActionResult> GetMyCompanyUsers()
        {
            var companyIdClaim = User.Claims.FirstOrDefault(c => c.Type == "companyId")?.Value;
            if (!int.TryParse(companyIdClaim, out int companyId))
                return BadRequest(new { success = false, message = "Company context missing" });

            var result = await authService.GetUsersByCompanyAsync(companyId);
            if (!result.Success)
                return BadRequest(new { success = false, message = result.Messages?.FirstOrDefault() });

            return Ok(new { success = true, data = result.Data });
        }

        [RequirePermission(PermissionCatalogKeys.Pages.Users, PermissionCatalogKeys.Actions.View)]
        [HttpGet("my-company/paged")]
        public async Task<IActionResult> GetMyCompanyUsersPage(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            CancellationToken cancellationToken = default)
        {
            var companyIdClaim = User.FindFirst("companyId")?.Value;
            if (!int.TryParse(companyIdClaim, out var companyId))
                return BadRequest(new { success = false, message = "Company context missing" });

            var result = await authService.GetUserManagementPageAsync(
                companyId,
                pageNumber,
                pageSize,
                search,
                cancellationToken);
            return result.Success
                ? Ok(new { success = true, data = result.Data })
                : StatusCode(result.StatusCode ?? StatusCodes.Status400BadRequest, result);
        }

        [RequirePermission(PermissionCatalogKeys.Pages.Users, PermissionCatalogKeys.Actions.Activate)]
        [HttpPost("{id}/toggle-status")]
        public async Task<IActionResult> ToggleStatus(string id, [FromQuery] bool isActive)
        {
            var result = await authService.ToggleUserStatusAsync(id, isActive, User);
            if (!result.Success)
                return StatusCode(
                    result.StatusCode ?? StatusCodes.Status400BadRequest,
                    new { success = false, message = result.Messages?.FirstOrDefault() });

            return Ok(new { success = true, data = result.Data });
        }
    }
}
