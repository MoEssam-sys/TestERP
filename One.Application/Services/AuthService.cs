using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using One.Domain.Constants;
using One.Domain.DTOs;
using One.Domain.DTOs.Identity;
using One.Domain.Entities.Identity;
using One.Infrastructure.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace One.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IAuthRepository _authRepository;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IConfiguration configuration,
            IAuthRepository authRepository,
            IEmailService emailService,
            ILogger<AuthService> logger)
        {
            _configuration = configuration;
            _authRepository = authRepository;
            _emailService = emailService;
            _logger = logger;
        }

        public Task<Response> RegisterAsync(
            RegisterUserDto registerUserDto,
            CancellationToken cancellationToken = default)
        {
            return RegisterCoreAsync(registerUserDto, SystemRoles.User, cancellationToken);
        }

        public async Task<Response> RegisterManagedAsync(
            CreateManagedUserRequest registerUserDto,
            ClaimsPrincipal currentUser,
            CancellationToken cancellationToken = default)
        {
            var requestedRole = registerUserDto.Role.Trim();

            if (currentUser.IsInRole(SystemRoles.SuperAdmin))
            {
                var allowedRoles = new[]
                {
                    SystemRoles.CompanyAdmin,
                    SystemRoles.User,
                    SystemRoles.Tester
                };

                if (!allowedRoles.Contains(requestedRole, StringComparer.Ordinal))
                    return Forbidden("The requested role cannot be assigned.");

                if (!registerUserDto.CompanyId.HasValue)
                    return BadRequest("Company is required.");

                return await RegisterCoreAsync(registerUserDto, requestedRole, cancellationToken);
            }

            if (!currentUser.IsInRole(SystemRoles.CompanyAdmin))
                return Forbidden("You are not allowed to create users.");

            if (!string.Equals(requestedRole, SystemRoles.User, StringComparison.Ordinal))
                return Forbidden("Company administrators can only create regular users.");

            var companyClaim = currentUser.FindFirst("companyId")?.Value;
            if (!int.TryParse(companyClaim, out var currentCompanyId))
                return Forbidden("Company context is missing.");

            if (registerUserDto.CompanyId.HasValue && registerUserDto.CompanyId.Value != currentCompanyId)
                return Forbidden("You cannot create a user for another company.");

            registerUserDto.CompanyId = currentCompanyId;
            return await RegisterCoreAsync(registerUserDto, SystemRoles.User, cancellationToken);
        }

        private async Task<Response> RegisterCoreAsync(
            RegisterUserDto registerUserDto,
            string role,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var username = registerUserDto.UserName.Trim();
            var email = registerUserDto.Email?.Trim();
            var existingUserName = await _authRepository.FindByUsernameAsync(username);
            if (existingUserName != null)
                return BadRequest("Username already exists.");

            if (!string.IsNullOrWhiteSpace(email) && await _authRepository.FindByEmailAsync(email) is not null)
                return BadRequest("Email already exists.");

            if (!await _authRepository.RoleExistsAsync(role))
                return BadRequest("Role does not exist.");

            if (!registerUserDto.CompanyId.HasValue)
                return BadRequest("Company is required.");

            var company = await _authRepository.GetCompanyByIdAsync(registerUserDto.CompanyId.Value);
            if (company is null || !company.IsActive)
                return BadRequest("Company does not exist or is inactive.");

            var user = new UserData
            {
                UserName = username,
                Email = email,
                FirstName = registerUserDto.FirstName,
                LastName = registerUserDto.LastName,
                Age = registerUserDto.Age,
                PhoneNumber = registerUserDto.PhoneNumber,
                SecurityStamp = Guid.NewGuid().ToString(),
                LockoutEnabled = true,
                CompanyId = registerUserDto.CompanyId
            };

            var result = await _authRepository.CreateUserInRoleAsync(
                user,
                registerUserDto.Password,
                role);
            if (!result.Succeeded)
                return new Response
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    Message = "Registration failed",
                    Messages = result.Errors.Select(e => e.Description).ToList()
                };

            return new Response { StatusCode = StatusCodes.Status200OK, Message = "User created successfully" };
        }

        private static Response BadRequest(string message) => new()
        {
            StatusCode = StatusCodes.Status400BadRequest,
            Message = message,
            Messages = [message]
        };

        private static Response Forbidden(string message) => new()
        {
            StatusCode = StatusCodes.Status403Forbidden,
            Message = message,
            Messages = [message]
        };

        public async Task<One.Domain.Base.Result<string>> LoginWithOtpAsync(string email, CancellationToken cancellationToken = default)
        {
            const string genericResponse = "If the account is eligible, a verification code will be sent.";
            try
            {
                var user = await _authRepository.FindByEmailAsync(email.Trim());
                if (user is null || !await IsUserAllowedAsync(user, cancellationToken))
                    return One.Domain.Base.Result<string>.Ok(genericResponse);

                var roles = await _authRepository.GetRolesAsync(user);
                if (!roles.Contains(SystemRoles.SuperAdmin, StringComparer.Ordinal))
                    return One.Domain.Base.Result<string>.Ok(genericResponse);

                var codeLength = _configuration.GetValue<int>("Authentication:Otp:CodeLength", 6);
                var lifetimeMinutes = _configuration.GetValue<int>("Authentication:Otp:LifetimeMinutes", 5);
                var cooldownSeconds = _configuration.GetValue<int>("Authentication:Otp:ResendCooldownSeconds", 60);
                var upperBound = checked((int)Math.Pow(10, codeLength));
                var code = RandomNumberGenerator.GetInt32(0, upperBound).ToString($"D{codeLength}");
                var hash = HashOtp(user.Id, code);
                var created = await _authRepository.TryCreateOtpChallengeAsync(
                    user.Id,
                    hash,
                    DateTime.UtcNow.AddMinutes(lifetimeMinutes),
                    TimeSpan.FromSeconds(cooldownSeconds),
                    cancellationToken);
                if (!created)
                    return One.Domain.Base.Result<string>.Ok(genericResponse);

                try
                {
                    await _emailService.SendEmailAsync(
                        email.Trim(),
                        "OneERP login verification code",
                        $"Your login code is: <b>{code}</b>. It expires in {lifetimeMinutes} minutes.");
                }
                catch
                {
                    await _authRepository.InvalidateOtpChallengeAsync(
                        user.Id,
                        hash,
                        cancellationToken);
                    throw;
                }

                return One.Domain.Base.Result<string>.Ok(genericResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send a login verification code.");
                return One.Domain.Base.Result<string>.Ok(genericResponse);
            }
        }

        public async Task<AuthResponseDto> VerifyOtpAsync(string email, string otp, CancellationToken cancellationToken = default)
        {
            var user = await _authRepository.FindByEmailAsync(email.Trim());
            if (user is null || !await IsUserAllowedAsync(user, cancellationToken))
                return InvalidLogin();

            var roles = await _authRepository.GetRolesAsync(user);
            if (!roles.Contains(SystemRoles.SuperAdmin, StringComparer.Ordinal))
                return InvalidLogin();

            var maxAttempts = _configuration.GetValue<int>("Authentication:Otp:MaxAttempts", 5);
            var valid = await _authRepository.ConsumeOtpChallengeAsync(
                user.Id,
                HashOtp(user.Id, otp.Trim()),
                maxAttempts,
                cancellationToken);
            if (!valid)
                return InvalidLogin();

            return await CreateAuthenticationResponseAsync(user, roles, cancellationToken);
        }

        public async Task<One.Domain.Base.Result<CompanyValidationDto>> ValidateCompanyAsync(string code, CancellationToken cancellationToken = default)
        {
            var company = await _authRepository.FindCompanyByCodeAsync(code);
            if (company == null)
                return One.Domain.Base.Result<CompanyValidationDto>.Fail(new List<string?> { "Invalid Company Code" });

            if (!company.IsActive)
                return One.Domain.Base.Result<CompanyValidationDto>.Fail(new List<string?> { "Company is inactive" });

            var isEmpty = await _authRepository.IsCompanyEmptyAsync(company.Id);

            return One.Domain.Base.Result<CompanyValidationDto>.Ok(new CompanyValidationDto
            {
                CompanyId = company.Id,
                Name = company.Name,
                RequiresAdminRegistration = isEmpty
            });
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto loginDto, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(loginDto.Email) && string.IsNullOrWhiteSpace(loginDto.UserName))
                return InvalidLogin();

            var user = !string.IsNullOrWhiteSpace(loginDto.Email)
                ? await _authRepository.FindByEmailAsync(loginDto.Email.Trim())
                : await _authRepository.FindByUsernameAsync(loginDto.UserName!.Trim());
            if (user is null || await _authRepository.IsLockedOutAsync(user))
                return InvalidLogin();

            if (!await IsUserAllowedForLoginScopeAsync(user, loginDto.CompanyCode, cancellationToken))
                return InvalidLogin();

            if (!await _authRepository.CheckPasswordAsync(user, loginDto.Password))
            {
                await _authRepository.AccessFailedAsync(user);
                return InvalidLogin();
            }

            await _authRepository.ResetAccessFailedCountAsync(user);
            var roles = await _authRepository.GetRolesAsync(user);
            return await CreateAuthenticationResponseAsync(user, roles, cancellationToken);
        }

        public async Task<One.Domain.Base.Result<string>> RequestPasswordResetAsync(
            PasswordResetRequestDto request,
            CancellationToken cancellationToken = default)
        {
            const string genericResponse = "If the account is eligible, a password reset link will be sent.";
            try
            {
                var email = request.Email.Trim();
                var user = await _authRepository.FindByEmailAsync(email);
                if (user is null ||
                    !await IsUserAllowedForLoginScopeAsync(user, request.CompanyCode, cancellationToken))
                {
                    return One.Domain.Base.Result<string>.Ok(genericResponse);
                }

                var token = await _authRepository.GeneratePasswordResetTokenAsync(user);
                var encodedToken = EncodeToken(token);
                var clientBaseUrl = _configuration["Authentication:PasswordReset:ClientBaseUrl"]
                    ?? throw new InvalidOperationException(
                        "Authentication:PasswordReset:ClientBaseUrl configuration value is missing.");
                var callbackUrl = string.Concat(
                    clientBaseUrl.TrimEnd('/'),
                    "/reset-password?email=", Uri.EscapeDataString(email),
                    "&company=", Uri.EscapeDataString(request.CompanyCode.Trim()),
                    "&token=", Uri.EscapeDataString(encodedToken));
                var safeUrl = WebUtility.HtmlEncode(callbackUrl);
                var lifetimeMinutes = _configuration.GetValue<int>(
                    "Authentication:PasswordReset:TokenMinutes",
                    20);
                var body = $"""
                    <div style="font-family:Arial,sans-serif;line-height:1.7;color:#16223b">
                      <h2>OneERP password reset</h2>
                      <p>A password reset was requested for your account.</p>
                      <p><a href="{safeUrl}">Reset password</a></p>
                      <p>This link expires in {lifetimeMinutes} minutes. If you did not request it, ignore this email.</p>
                      <hr />
                      <div dir="rtl">
                        <h2>إعادة تعيين كلمة مرور OneERP</h2>
                        <p>تم طلب إعادة تعيين كلمة المرور لحسابك.</p>
                        <p><a href="{safeUrl}">تعيين كلمة مرور جديدة</a></p>
                        <p>تنتهي صلاحية الرابط خلال {lifetimeMinutes} دقيقة. تجاهل الرسالة إذا لم تطلب ذلك.</p>
                      </div>
                    </div>
                    """;

                await _emailService.SendEmailAsync(
                    email,
                    "OneERP password reset | إعادة تعيين كلمة المرور",
                    body);
                return One.Domain.Base.Result<string>.Ok(genericResponse);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to send a password reset link.");
                return One.Domain.Base.Result<string>.Ok(genericResponse);
            }
        }

        public async Task<One.Domain.Base.Result<string>> ResetPasswordAsync(
            PasswordResetConfirmDto request,
            CancellationToken cancellationToken = default)
        {
            if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
                return One.Domain.Base.Result<string>.Fail(["Passwords do not match."]);

            var user = await _authRepository.FindByEmailAsync(request.Email.Trim());
            if (user is null ||
                !await IsUserAllowedForLoginScopeAsync(user, request.CompanyCode, cancellationToken) ||
                !TryDecodeToken(request.Token, out var token))
            {
                return One.Domain.Base.Result<string>.Fail(["The password reset link is invalid or expired."]);
            }

            var reset = await _authRepository.ResetPasswordAsync(
                user,
                token,
                request.NewPassword,
                cancellationToken);
            if (!reset.Succeeded)
            {
                var invalidToken = reset.Errors.Any(error =>
                    string.Equals(error.Code, "InvalidToken", StringComparison.Ordinal));
                var messages = invalidToken
                    ? new List<string?> { "The password reset link is invalid or expired." }
                    : reset.Errors.Select(error => (string?)error.Description).ToList();
                return One.Domain.Base.Result<string>.Fail(messages);
            }

            await _authRepository.RevokeAllSessionsAsync(user.Id, cancellationToken);
            return One.Domain.Base.Result<string>.Ok("Password reset successfully.");
        }

        public async Task<AuthResponseDto> RefreshSessionAsync(
            RefreshTokenRequest request,
            CancellationToken cancellationToken = default)
        {
            var refreshDays = _configuration.GetValue<int>("Authentication:RefreshTokenDays", 14);
            var rotation = await _authRepository.RotateSessionAsync(
                request.RefreshToken,
                TimeSpan.FromDays(refreshDays),
                cancellationToken);
            if (!rotation.Success || rotation.User is null || rotation.Session is null)
                return InvalidLogin();

            var roles = await _authRepository.GetRolesAsync(rotation.User);
            return CreateAuthenticationResponse(rotation.User, roles, rotation.Session);
        }

        public async Task RevokeCurrentSessionAsync(
            ClaimsPrincipal currentUser,
            CancellationToken cancellationToken = default)
        {
            var userId = currentUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var sessionIdValue = currentUser.FindFirst("sid")?.Value;
            if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(sessionIdValue, out var sessionId))
                return;

            await _authRepository.RevokeSessionAsync(sessionId, userId, cancellationToken);
        }

        public async Task<One.Domain.Base.Result<List<One.Domain.DTOs.CompanySummaryDto>>> GetCompanySummariesAsync(CancellationToken cancellationToken = default)
        {
            var data = await _authRepository.GetCompanySummariesAsync(cancellationToken);
            return One.Domain.Base.Result<List<One.Domain.DTOs.CompanySummaryDto>>.Ok(data.ToList());
        }

        public async Task<One.Domain.Base.Result<One.Domain.DTOs.CompanyDetailsDto>> GetCompanyDetailsAsync(int id, CancellationToken cancellationToken = default)
        {
            var company = await _authRepository.GetCompanyDetailsAsync(id, cancellationToken);
            return company is null
                ? One.Domain.Base.Result<One.Domain.DTOs.CompanyDetailsDto>.Fail(["Company not found"])
                : One.Domain.Base.Result<One.Domain.DTOs.CompanyDetailsDto>.Ok(company);
        }

        public async Task<UserInfoDto> GetCurrentUserAsync(ClaimsPrincipal userClaims, CancellationToken cancellationToken = default)
        {
            var userId = userClaims.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;

            if (string.IsNullOrWhiteSpace(userId))
                return new();

            var user = await _authRepository.GetUserByIdAsync(userId);
            if (user == null)
                return new();

            return new UserInfoDto
            {
                UserName = user.UserName ?? "",
                Email = user.Email ?? "",
                FirstName = user.FirstName,
                LastName = user.LastName,
                Age = user.Age,
                PhoneNumber = user.PhoneNumber ?? "",
            };
        }


        public async Task<One.Domain.Base.Result<List<UserManagementDto>>> GetAllUsersAsync(CancellationToken cancellationToken = default)
        {
            var users = await _authRepository.GetUserManagementAsync(null, cancellationToken);
            return One.Domain.Base.Result<List<UserManagementDto>>.Ok(users);
        }

        public async Task<One.Domain.Base.Result<List<UserManagementDto>>> GetUsersByCompanyAsync(int companyId, CancellationToken cancellationToken = default)
        {
            var users = await _authRepository.GetUserManagementAsync(companyId, cancellationToken);
            return One.Domain.Base.Result<List<UserManagementDto>>.Ok(users);
        }

        public async Task<One.Domain.Base.Result<One.Domain.Base.PagedResult<UserManagementDto>>> GetUserManagementPageAsync(
            int? companyId,
            int pageNumber,
            int pageSize,
            string? search,
            CancellationToken cancellationToken = default)
        {
            var users = await _authRepository.GetUserManagementPageAsync(
                companyId,
                pageNumber,
                pageSize,
                search,
                cancellationToken);
            return One.Domain.Base.Result<One.Domain.Base.PagedResult<UserManagementDto>>.Ok(users);
        }

        public async Task<One.Domain.Base.Result<bool>> ToggleUserStatusAsync(
            string userId,
            bool isActive,
            ClaimsPrincipal currentUser,
            CancellationToken cancellationToken = default)
        {
            var targetUser = await _authRepository.GetUserByIdAsync(userId);
            if (targetUser is null)
                return One.Domain.Base.Result<bool>.Fail(["User not found."], 404);

            if (!currentUser.IsInRole(SystemRoles.SuperAdmin))
            {
                var companyClaim = currentUser.FindFirst("companyId")?.Value;
                if (!int.TryParse(companyClaim, out var currentCompanyId) || currentCompanyId <= 0)
                    return One.Domain.Base.Result<bool>.Fail(["Company context is missing."], 403);

                if (targetUser.CompanyId != currentCompanyId)
                    return One.Domain.Base.Result<bool>.Fail(["You cannot manage a user from another company."], 403);
            }

            var success = await _authRepository.ToggleUserStatusAsync(userId, isActive);
            if (!success)
                return One.Domain.Base.Result<bool>.Fail(["User update failed."]);

            return One.Domain.Base.Result<bool>.Ok(true);
        }

        public async Task<One.Domain.Base.Result<bool>> ToggleCompanyStatusAsync(int companyId, bool isActive, CancellationToken cancellationToken = default)
        {
            var success = await _authRepository.ToggleCompanyStatusAsync(companyId, isActive);
            if (!success) return One.Domain.Base.Result<bool>.Fail(new List<string?> { "Company not found or update failed" });
            return One.Domain.Base.Result<bool>.Ok(true);
        }

        #region Helper Methods

        private async Task<AuthResponseDto> CreateAuthenticationResponseAsync(
            UserData user,
            IList<string> roles,
            CancellationToken cancellationToken)
        {
            var refreshDays = _configuration.GetValue<int>("Authentication:RefreshTokenDays", 14);
            var session = await _authRepository.CreateSessionAsync(
                user,
                TimeSpan.FromDays(refreshDays),
                cancellationToken);
            return CreateAuthenticationResponse(user, roles, session);
        }

        private AuthResponseDto CreateAuthenticationResponse(
            UserData user,
            IList<string> roles,
            UserSessionIssue session)
        {
            var claims = new List<Claim>
            {
                new("firstName", user.FirstName ?? string.Empty),
                new("lastName", user.LastName ?? string.Empty),
                new("userId", user.Id),
                new("companyId", user.CompanyId?.ToString() ?? string.Empty),
                new("sid", session.SessionId.ToString()),
                new(ClaimTypes.NameIdentifier, user.Id),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
            var token = GetToken(claims);
            return new AuthResponseDto
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Login successful",
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                Expiration = token.ValidTo,
                RefreshToken = session.RefreshToken,
                RefreshTokenExpiration = session.ExpiresAtUtc
            };
        }

        private async Task<bool> IsUserAllowedAsync(
            UserData user,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!user.IsActive)
                return false;
            if (!user.CompanyId.HasValue)
                return true;

            var company = await _authRepository.GetCompanyByIdAsync(user.CompanyId.Value);
            return company?.IsActive == true;
        }

        private async Task<bool> IsUserAllowedForLoginScopeAsync(
            UserData user,
            string? companyCode,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!user.IsActive)
                return false;

            var normalizedCode = companyCode?.Trim();
            if (!user.CompanyId.HasValue)
                return string.IsNullOrWhiteSpace(normalizedCode);
            if (string.IsNullOrWhiteSpace(normalizedCode))
                return false;

            var company = await _authRepository.GetCompanyByIdAsync(user.CompanyId.Value);
            return company?.IsActive == true &&
                string.Equals(company.Code, normalizedCode, StringComparison.OrdinalIgnoreCase);
        }

        private static string EncodeToken(string token) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(token))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

        private static bool TryDecodeToken(string encodedToken, out string token)
        {
            token = string.Empty;
            try
            {
                var value = encodedToken.Replace('-', '+').Replace('_', '/');
                value = value.PadRight(value.Length + ((4 - value.Length % 4) % 4), '=');
                token = Encoding.UTF8.GetString(Convert.FromBase64String(value));
                return !string.IsNullOrWhiteSpace(token);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private string HashOtp(string userId, string code)
        {
            var pepper = _configuration["Security:OtpPepper"]
                ?? throw new InvalidOperationException("Security:OtpPepper configuration value is missing.");
            var payload = Encoding.UTF8.GetBytes($"{userId}\0{code}");
            return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(pepper), payload));
        }

        private static AuthResponseDto InvalidLogin() => new()
        {
            StatusCode = StatusCodes.Status401Unauthorized,
            Message = "Invalid login attempt."
        };

        private JwtSecurityToken GetToken(List<Claim> claims)
        {
            var secret = _configuration["JWT:Secret"];
            if (string.IsNullOrEmpty(secret))
                throw new InvalidOperationException("JWT:Secret configuration value is missing or empty.");

            var accessTokenMinutes = _configuration.GetValue<int>("Authentication:AccessTokenMinutes", 30);
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            return new JwtSecurityToken(
                issuer: _configuration["JWT:ValidIssuer"],
                audience: _configuration["JWT:ValidAudience"],
                expires: DateTime.UtcNow.AddMinutes(accessTokenMinutes),
                claims: claims,
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        }

        #endregion Helper Methods
    }
}
