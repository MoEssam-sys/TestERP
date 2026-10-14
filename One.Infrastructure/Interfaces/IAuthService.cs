using One.Domain.DTOs;
using One.Domain.DTOs.Identity;
using One.Domain.Base;
using System.Security.Claims;

namespace One.Infrastructure.Interfaces
{
    public interface IAuthService
    {
        Task<Response> RegisterAsync(RegisterUserDto dto, CancellationToken cancellationToken = default);

        Task<Response> RegisterManagedAsync(
            CreateManagedUserRequest dto,
            ClaimsPrincipal currentUser,
            CancellationToken cancellationToken = default);

        Task<AuthResponseDto> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default);
        Task<Result<string>> RequestPasswordResetAsync(
            PasswordResetRequestDto request,
            CancellationToken cancellationToken = default);
        Task<Result<string>> ResetPasswordAsync(
            PasswordResetConfirmDto request,
            CancellationToken cancellationToken = default);
        Task<AuthResponseDto> RefreshSessionAsync(
            RefreshTokenRequest request,
            CancellationToken cancellationToken = default);
        Task RevokeCurrentSessionAsync(
            ClaimsPrincipal currentUser,
            CancellationToken cancellationToken = default);
        Task<One.Domain.Base.Result<List<One.Domain.DTOs.CompanySummaryDto>>> GetCompanySummariesAsync(CancellationToken cancellationToken = default);
        Task<One.Domain.Base.Result<One.Domain.DTOs.CompanyDetailsDto>> GetCompanyDetailsAsync(int id, CancellationToken cancellationToken = default);

        Task<UserInfoDto> GetCurrentUserAsync(ClaimsPrincipal userClaims, CancellationToken cancellationToken = default);

        Task<One.Domain.Base.Result<CompanyValidationDto>> ValidateCompanyAsync(string code, CancellationToken cancellationToken = default);

        Task<One.Domain.Base.Result<string>> LoginWithOtpAsync(string email, CancellationToken cancellationToken = default);

        Task<AuthResponseDto> VerifyOtpAsync(string email, string otp, CancellationToken cancellationToken = default);

        // Management
        Task<One.Domain.Base.Result<List<UserManagementDto>>> GetAllUsersAsync(CancellationToken cancellationToken = default);
        Task<One.Domain.Base.Result<List<UserManagementDto>>> GetUsersByCompanyAsync(int companyId, CancellationToken cancellationToken = default);
        Task<Result<PagedResult<UserManagementDto>>> GetUserManagementPageAsync(
            int? companyId,
            int pageNumber,
            int pageSize,
            string? search,
            CancellationToken cancellationToken = default);
        Task<One.Domain.Base.Result<bool>> ToggleUserStatusAsync(
            string userId,
            bool isActive,
            ClaimsPrincipal currentUser,
            CancellationToken cancellationToken = default);
        Task<One.Domain.Base.Result<bool>> ToggleCompanyStatusAsync(int companyId, bool isActive, CancellationToken cancellationToken = default);
    }
}
