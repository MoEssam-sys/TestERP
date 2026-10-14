using Microsoft.AspNetCore.Identity;
using One.Domain.DTOs;
using One.Domain.Base;
using One.Domain.Entities.Identity;
using System.Security.Claims;

namespace One.Infrastructure.Interfaces
{
    public sealed record UserSessionIssue(
        Guid SessionId,
        string RefreshToken,
        DateTime ExpiresAtUtc);

    public sealed record UserSessionRotation(
        bool Success,
        UserData? User,
        UserSessionIssue? Session);

    public interface IAuthRepository
    {
        Task<UserData?> FindByEmailAsync(string email);

        Task<UserData?> FindByUsernameAsync(string username);

        Task<IdentityResult> CreateUserAsync(UserData user, string password);

        Task<IdentityResult> CreateUserInRoleAsync(UserData user, string password, string role);

        Task<bool> CheckPasswordAsync(UserData user, string password);
        Task<bool> IsLockedOutAsync(UserData user);
        Task AccessFailedAsync(UserData user);
        Task ResetAccessFailedCountAsync(UserData user);

        Task<string> GeneratePasswordResetTokenAsync(UserData user);

        Task<IdentityResult> ResetPasswordAsync(
            UserData user,
            string token,
            string newPassword,
            CancellationToken cancellationToken = default);

        Task<IList<string>> GetRolesAsync(UserData user);

        Task<IdentityResult> AddToRoleAsync(UserData user, string role);

        Task<bool> RoleExistsAsync(string role);

        Task<UserData?> GetUserByIdAsync(string userId);

        // Company
        Task<Company?> FindCompanyByCodeAsync(string code);
        Task<bool> IsCompanyEmptyAsync(int companyId);
        Task<IList<Company>> GetAllCompaniesAsync();
        Task<IReadOnlyList<CompanySummaryDto>> GetCompanySummariesAsync(CancellationToken cancellationToken = default);
        Task<PagedResult<CompanySummaryDto>> GetCompanySummaryPageAsync(
            int pageNumber,
            int pageSize,
            string? search,
            CancellationToken cancellationToken = default);
        Task<IReadOnlyList<CompanyLookupDto>> SearchCompaniesAsync(
            string? search,
            int take,
            CancellationToken cancellationToken = default);
        Task<CompanyDashboardDto> GetCompanyDashboardAsync(
            string? search,
            int take,
            CancellationToken cancellationToken = default);
        Task<CompanyDetailsDto?> GetCompanyDetailsAsync(int id, CancellationToken cancellationToken = default);
        Task<Company?> GetCompanyByIdAsync(int id);

        Task UpdateCompanyAsync(Company company);

        // OTP & Roles
        Task<bool> TryCreateOtpChallengeAsync(
            string userId,
            string otpHash,
            DateTime expiresAtUtc,
            TimeSpan resendCooldown,
            CancellationToken cancellationToken = default);
        Task<bool> ConsumeOtpChallengeAsync(
            string userId,
            string otpHash,
            int maxAttempts,
            CancellationToken cancellationToken = default);
        Task InvalidateOtpChallengeAsync(
            string userId,
            string otpHash,
            CancellationToken cancellationToken = default);
        Task<UserSessionIssue> CreateSessionAsync(
            UserData user,
            TimeSpan lifetime,
            CancellationToken cancellationToken = default);
        Task<UserSessionRotation> RotateSessionAsync(
            string refreshToken,
            TimeSpan lifetime,
            CancellationToken cancellationToken = default);
        Task RevokeSessionAsync(
            Guid sessionId,
            string userId,
            CancellationToken cancellationToken = default);
        Task RevokeAllSessionsAsync(
            string userId,
            CancellationToken cancellationToken = default);
        Task<IdentityResult> AddToRoleAsync(UserData user, string role, int? companyId = null);

        // Management
        Task<bool> ToggleUserStatusAsync(string userId, bool isActive);
        Task<bool> ToggleCompanyStatusAsync(int companyId, bool isActive);
        Task<List<UserManagementDto>> GetUserManagementAsync(
            int? companyId,
            CancellationToken cancellationToken = default);
        Task<PagedResult<UserManagementDto>> GetUserManagementPageAsync(
            int? companyId,
            int pageNumber,
            int pageSize,
            string? search,
            CancellationToken cancellationToken = default);
    }
}
