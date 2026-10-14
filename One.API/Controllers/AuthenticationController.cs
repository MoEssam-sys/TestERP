using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using One.API.Authorization;
using One.Domain.Constants;
using One.Domain.DTOs.Identity;
using One.Domain.Localization;
using One.Infrastructure.DbContext.MainDbContext;
using One.Infrastructure.Interfaces;
using Microsoft.AspNetCore.RateLimiting;

namespace One.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationController(IAuthService authService, ITranslator loc) : ControllerBase
    {
        //private readonly IDapperConnectionFactory _dbFactory;

        [HttpPost("Register")]
        public async Task<IActionResult> Register(
            [FromBody] RegisterUserDto registerUser,
            [FromQuery(Name = "role")] string? requestedRole,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!string.IsNullOrWhiteSpace(requestedRole))
                return BadRequest(new { success = false, message = "The public registration role cannot be selected." });

            var result = await authService.RegisterAsync(registerUser, cancellationToken);

            return RegistrationResult(result);
        }

        [RequirePermission(PermissionCatalogKeys.Pages.RegisterUser, PermissionCatalogKeys.Actions.Create)]
        [HttpPost("managed-users")]
        public async Task<IActionResult> RegisterManagedUser(
            [FromBody] CreateManagedUserRequest registerUser,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await authService.RegisterManagedAsync(registerUser, User, cancellationToken);

            return RegistrationResult(result);
        }

        private IActionResult RegistrationResult(One.Domain.DTOs.Response result)
        {
            if (result.StatusCode == StatusCodes.Status403Forbidden)
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = result.Message, messages = result.Messages });

            if (result.StatusCode == StatusCodes.Status400BadRequest)
                return BadRequest(new { success = false, message = result.Message, messages = result.Messages });

            if (result.StatusCode == StatusCodes.Status401Unauthorized)
                return Unauthorized(new { success = false, message = result.Message });

            if (result.StatusCode >= StatusCodes.Status500InternalServerError)
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { success = false, message = "The request could not be completed." });

            return Ok(new { success = true, message = loc["RegisteredSuccessfully"], data = result });
        }

        [HttpPost("Login")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginDto login,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await authService.LoginAsync(login, cancellationToken);

            if (result.StatusCode == StatusCodes.Status400BadRequest)
                return BadRequest(new { success = false, message = result.Message });

            if (result.StatusCode == StatusCodes.Status401Unauthorized)
                return Unauthorized(new { success = false, message = result.Message });

            return Ok(new { success = true, message = "Login successful", data = result });
        }

        [HttpPost("Password/Forgot")]
        [EnableRateLimiting("otp-request")]
        public async Task<IActionResult> ForgotPassword(
            [FromBody] PasswordResetRequestDto request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await authService.RequestPasswordResetAsync(request, cancellationToken);
            return Ok(new { success = true, message = result.Data });
        }

        [HttpPost("Password/Reset")]
        [EnableRateLimiting("otp-verify")]
        public async Task<IActionResult> ResetPassword(
            [FromBody] PasswordResetConfirmDto request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await authService.ResetPasswordAsync(request, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(new
                {
                    success = false,
                    message = result.Messages?.FirstOrDefault(),
                    messages = result.Messages
                });
            }

            return Ok(new { success = true, message = result.Data, data = result.Data });
        }

        [Authorize]
        [HttpGet("GetCurrentUser")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var user = await authService.GetCurrentUserAsync(User);
            if (user == null || string.IsNullOrWhiteSpace(user.FirstName))
                return Unauthorized(new { message = "User not found or invalid token" });

            return Ok(user);
        }

        /// <summary>
        /// Triggers OTP generation and email for Super Admin login
        /// </summary>
        [HttpPost("Login/Otp")]
        [EnableRateLimiting("otp-request")]
        public async Task<IActionResult> RequestOtp(
            [FromBody] OtpRequestDto request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { success = false, message = "Email is required" });

            var result = await authService.LoginWithOtpAsync(request.Email, cancellationToken);

            return Ok(new { success = true, message = result.Data });
        }

        /// <summary>
        /// Verifies OTP and returns JWT token for Super Admin
        /// </summary>
        [HttpPost("Login/Verify")]
        [EnableRateLimiting("otp-verify")]
        public async Task<IActionResult> VerifyOtp(
            [FromBody] OtpVerifyDto request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Otp))
                return BadRequest(new { success = false, message = "Email and OTP are required" });

            var result = await authService.VerifyOtpAsync(request.Email, request.Otp, cancellationToken);

            if (result.StatusCode != StatusCodes.Status200OK)
                return Unauthorized(new { success = false, message = result.Message });

            return Ok(new { success = true, message = "Login successful", data = result });
        }

        [HttpPost("Refresh")]
        [EnableRateLimiting("refresh")]
        public async Task<IActionResult> Refresh(
            [FromBody] RefreshTokenRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await authService.RefreshSessionAsync(request, cancellationToken);
            return result.StatusCode == StatusCodes.Status200OK
                ? Ok(new { success = true, data = result })
                : Unauthorized(new { success = false, message = result.Message });
        }

        [Authorize]
        [HttpPost("Logout")]
        public async Task<IActionResult> Logout(CancellationToken cancellationToken)
        {
            await authService.RevokeCurrentSessionAsync(User, cancellationToken);
            return Ok(new { success = true, data = true });
        }

        /// <summary>
        /// Validates a company code for onboarding flow
        /// </summary>
        [HttpGet("ValidateCompany/{code}")]
        public async Task<IActionResult> ValidateCompany(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return BadRequest(new { success = false, message = "Company code is required" });

            var result = await authService.ValidateCompanyAsync(code);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Messages?.FirstOrDefault() });

            return Ok(new { success = true, data = result.Data });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("Companies/Summary")]
        public async Task<IActionResult> GetCompanySummaries()
        {
            var result = await authService.GetCompanySummariesAsync();
            if (!result.Success)
                return BadRequest(new { success = false, message = result.Messages?.FirstOrDefault() });

            return Ok(new { success = true, data = result.Data });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("Companies/{id}")]
        public async Task<IActionResult> GetCompanyDetails(int id)
        {
            var result = await authService.GetCompanyDetailsAsync(id);
            if (!result.Success)
                return NotFound(new { success = false, message = result.Messages?.FirstOrDefault() });

            return Ok(new { success = true, data = result.Data });
        }

    }
}
