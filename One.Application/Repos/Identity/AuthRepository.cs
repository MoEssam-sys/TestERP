using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using One.Domain.Base;
using One.Domain.DTOs;
using One.Domain.Entities.Identity;
using One.Infrastructure.Interfaces;
using One.Application.Services.Subscriptions;
using System.Data;
using System.Security.Cryptography;

namespace One.Application.Repos.Identity
{
    public class AuthRepository : IAuthRepository
    {
        private readonly UserManager<UserData> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly One.Infrastructure.DbContext.UsersDbContext.UsersDbContext _context;

        public AuthRepository(UserManager<UserData> userManager, RoleManager<IdentityRole> roleManager, One.Infrastructure.DbContext.UsersDbContext.UsersDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        public async Task<UserData?> FindByEmailAsync(string email)
            => await _userManager.FindByEmailAsync(email);

        public async Task<UserData?> FindByUsernameAsync(string username)
            => await _userManager.FindByNameAsync(username);

        public async Task<IdentityResult> CreateUserAsync(UserData user, string password)
            => await _userManager.CreateAsync(user, password);

        public async Task<IdentityResult> CreateUserInRoleAsync(
            UserData user,
            string password,
            string role)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var createResult = await _userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
                return createResult;

            var roleResult = await _userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return roleResult;
            }

            await transaction.CommitAsync();
            return IdentityResult.Success;
        }

        public async Task<bool> CheckPasswordAsync(UserData user, string password)
            => await _userManager.CheckPasswordAsync(user, password);

        public Task<bool> IsLockedOutAsync(UserData user) =>
            _userManager.IsLockedOutAsync(user);

        public async Task AccessFailedAsync(UserData user) =>
            _ = await _userManager.AccessFailedAsync(user);

        public async Task ResetAccessFailedCountAsync(UserData user) =>
            _ = await _userManager.ResetAccessFailedCountAsync(user);

        public Task<string> GeneratePasswordResetTokenAsync(UserData user) =>
            _userManager.GeneratePasswordResetTokenAsync(user);

        public async Task<IdentityResult> ResetPasswordAsync(
            UserData user,
            string token,
            string newPassword,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _userManager.ResetPasswordAsync(user, token, newPassword);
        }

        public async Task<IList<string>> GetRolesAsync(UserData user)
            => await _userManager.GetRolesAsync(user);

        public async Task<IdentityResult> AddToRoleAsync(UserData user, string role)
            => await _userManager.AddToRoleAsync(user, role);

        public async Task<bool> RoleExistsAsync(string role)
            => await _roleManager.RoleExistsAsync(role);

        public async Task<UserData?> GetUserByIdAsync(string userId)
            => await _userManager.FindByIdAsync(userId);

        // Company
        public async Task<Company?> FindCompanyByCodeAsync(string code)
        {
            return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(_context.Companies, c => c.Code == code);
        }

        public async Task<bool> IsCompanyEmptyAsync(int companyId)
        {
            // Use EF Core AnyAsync
            return !await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(_context.Users, u => u.CompanyId == companyId);
        }

        public async Task<IList<Company>> GetAllCompaniesAsync()
        {
            return await _context.Companies
                .AsNoTracking()
                .OrderBy(company => company.Name)
                .Take(100)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<CompanySummaryDto>> GetCompanySummariesAsync(
            CancellationToken cancellationToken = default)
        {
            return await _context.Companies
                .AsNoTracking()
                .OrderBy(company => company.Name)
                .Take(100)
                .Select(company => new CompanySummaryDto
                {
                    Id = company.Id,
                    Name = company.Name,
                    Code = company.Code,
                    IsActive = company.IsActive,
                    UsersCount = company.Users.Count,
                    AdminsCount = (
                        from user in _context.Users
                        join userRole in _context.UserRoles on user.Id equals userRole.UserId
                        join role in _context.Roles on userRole.RoleId equals role.Id
                        where user.CompanyId == company.Id && role.NormalizedName == "COMPANYADMIN"
                        select user.Id).Distinct().Count(),
                    CreatedOn = company.CreatedOn
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<PagedResult<CompanySummaryDto>> GetCompanySummaryPageAsync(
            int pageNumber,
            int pageSize,
            string? search,
            CancellationToken cancellationToken = default)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var query = FilterCompanies(search);
            var totalCount = await query.CountAsync(cancellationToken);
            var items = await ProjectCompanySummaries(query)
                .OrderBy(company => company.Name)
                .ThenBy(company => company.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<CompanySummaryDto>(items, totalCount, pageNumber, pageSize);
        }

        public async Task<IReadOnlyList<CompanyLookupDto>> SearchCompaniesAsync(
            string? search,
            int take,
            CancellationToken cancellationToken = default)
        {
            take = Math.Clamp(take, 1, 50);
            return await FilterCompanies(search)
                .OrderBy(company => company.Name)
                .ThenBy(company => company.Id)
                .Take(take)
                .Select(company => new CompanyLookupDto
                {
                    Id = company.Id,
                    Name = company.Name,
                    Code = company.Code
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<CompanyDashboardDto> GetCompanyDashboardAsync(
            string? search,
            int take,
            CancellationToken cancellationToken = default)
        {
            take = Math.Clamp(take, 1, 24);
            var totalCompanies = await _context.Companies
                .AsNoTracking()
                .CountAsync(cancellationToken);
            var totalUsers = await _context.Users
                .AsNoTracking()
                .Where(user => user.CompanyId.HasValue)
                .CountAsync(cancellationToken);
            var totalAdmins = await (
                from user in _context.Users.AsNoTracking()
                join userRole in _context.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
                join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where role.NormalizedName == "COMPANYADMIN"
                select user.Id)
                .Distinct()
                .CountAsync(cancellationToken);
            var companies = await ProjectCompanySummaries(FilterCompanies(search))
                .OrderByDescending(company => company.CreatedOn)
                .ThenBy(company => company.Id)
                .Take(take)
                .ToListAsync(cancellationToken);

            return new CompanyDashboardDto
            {
                TotalCompanies = totalCompanies,
                TotalUsers = totalUsers,
                TotalAdmins = totalAdmins,
                Companies = companies
            };
        }

        private IQueryable<Company> FilterCompanies(string? search)
        {
            var query = _context.Companies.AsNoTracking();
            if (string.IsNullOrWhiteSpace(search))
                return query;

            var term = search.Trim();
            return query.Where(company => company.Name.Contains(term) || company.Code.Contains(term));
        }

        private IQueryable<CompanySummaryDto> ProjectCompanySummaries(IQueryable<Company> query)
        {
            return query.Select(company => new CompanySummaryDto
            {
                Id = company.Id,
                Name = company.Name,
                Code = company.Code,
                IsActive = company.IsActive,
                UsersCount = company.Users.Count,
                AdminsCount = (
                    from user in _context.Users
                    join userRole in _context.UserRoles on user.Id equals userRole.UserId
                    join role in _context.Roles on userRole.RoleId equals role.Id
                    where user.CompanyId == company.Id && role.NormalizedName == "COMPANYADMIN"
                    select user.Id).Distinct().Count(),
                CreatedOn = company.CreatedOn
            });
        }

        public async Task<CompanyDetailsDto?> GetCompanyDetailsAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var company = await _context.Companies
                .AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new CompanyDetailsDto
                {
                    Id = item.Id,
                    Name = item.Name,
                    Code = item.Code,
                    IsActive = item.IsActive,
                    CreatedOn = item.CreatedOn,
                    UsersCount = item.Users.Count
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (company is null)
                return null;

            var users = await _context.Users
                .AsNoTracking()
                .Where(user => user.CompanyId == id)
                .OrderBy(user => user.UserName)
                .Select(user => new UserDetailDto
                {
                    Id = user.Id,
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    PhoneNumber = user.PhoneNumber ?? string.Empty,
                    IsActive = user.IsActive
                })
                .ToListAsync(cancellationToken);
            var roleRows = await (
                from user in _context.Users.AsNoTracking()
                join userRole in _context.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
                join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where user.CompanyId == id && role.Name != null
                select new { user.Id, Role = role.Name! })
                .ToListAsync(cancellationToken);
            var rolesByUser = roleRows.ToLookup(item => item.Id, item => item.Role);
            foreach (var user in users)
                user.Roles = rolesByUser[user.Id].ToList();

            company.Users = users;
            company.AdminsCount = roleRows
                .Where(item => string.Equals(item.Role, "CompanyAdmin", StringComparison.Ordinal))
                .Select(item => item.Id)
                .Distinct(StringComparer.Ordinal)
                .Count();
            return company;
        }


        public async Task<Company?> GetCompanyByIdAsync(int id)
        {
            return await _context.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task UpdateCompanyAsync(Company company)
        {
            _context.Companies.Update(company);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> TryCreateOtpChallengeAsync(
            string userId,
            string otpHash,
            DateTime expiresAtUtc,
            TimeSpan resendCooldown,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var latest = await _context.OtpChallenges
                .Where(challenge => challenge.UserId == userId)
                .OrderByDescending(challenge => challenge.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
            if (latest is not null && latest.LastSentAtUtc > now.Subtract(resendCooldown))
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            var activeChallenges = await _context.OtpChallenges
                .Where(challenge => challenge.UserId == userId && challenge.UsedAtUtc == null)
                .ToListAsync(cancellationToken);
            foreach (var challenge in activeChallenges)
                challenge.UsedAtUtc = now;

            _context.OtpChallenges.Add(new OtpChallenge
            {
                UserId = userId,
                OtpHash = otpHash,
                ExpiresAtUtc = expiresAtUtc,
                Attempts = 0,
                CreatedAtUtc = now,
                LastSentAtUtc = now
            });
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        public async Task<bool> ConsumeOtpChallengeAsync(
            string userId,
            string otpHash,
            int maxAttempts,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var challenge = await _context.OtpChallenges
                .Where(item =>
                    item.UserId == userId &&
                    item.UsedAtUtc == null &&
                    item.ExpiresAtUtc > now)
                .OrderByDescending(item => item.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
            if (challenge is null || challenge.Attempts >= maxAttempts)
            {
                if (challenge is not null)
                {
                    challenge.UsedAtUtc = now;
                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                else
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                return false;
            }

            var matches = FixedTimeEquals(challenge.OtpHash, otpHash);
            challenge.Attempts++;
            if (matches || challenge.Attempts >= maxAttempts)
                challenge.UsedAtUtc = now;
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return matches;
        }

        public async Task InvalidateOtpChallengeAsync(
            string userId,
            string otpHash,
            CancellationToken cancellationToken = default)
        {
            var challenge = await _context.OtpChallenges.FirstOrDefaultAsync(
                item => item.UserId == userId && item.OtpHash == otpHash && item.UsedAtUtc == null,
                cancellationToken);
            if (challenge is null)
                return;

            challenge.UsedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<UserSessionIssue> CreateSessionAsync(
            UserData user,
            TimeSpan lifetime,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var refreshToken = CreateRefreshToken();
            var session = new UserSession
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RefreshTokenHash = HashRefreshToken(refreshToken),
                SecurityStamp = user.SecurityStamp ?? string.Empty,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.Add(lifetime)
            };
            _context.UserSessions.Add(session);
            await _context.SaveChangesAsync(cancellationToken);
            return new UserSessionIssue(session.Id, refreshToken, session.ExpiresAtUtc);
        }

        public async Task<UserSessionRotation> RotateSessionAsync(
            string refreshToken,
            TimeSpan lifetime,
            CancellationToken cancellationToken = default)
        {
            var hash = HashRefreshToken(refreshToken);
            var now = DateTime.UtcNow;
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var current = await _context.UserSessions
                .Include(session => session.User)
                .ThenInclude(user => user.Company)
                .FirstOrDefaultAsync(session => session.RefreshTokenHash == hash, cancellationToken);
            if (current is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new UserSessionRotation(false, null, null);
            }

            if (current.RevokedAtUtc.HasValue)
            {
                await _context.UserSessions
                    .Where(session => session.UserId == current.UserId && session.RevokedAtUtc == null)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(
                        session => session.RevokedAtUtc,
                        (DateTime?)now), cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new UserSessionRotation(false, null, null);
            }

            if (current.ExpiresAtUtc <= now)
            {
                current.RevokedAtUtc = now;
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new UserSessionRotation(false, null, null);
            }

            var user = current.User;
            var validUser = user.IsActive &&
                (user.CompanyId is null || user.Company?.IsActive == true) &&
                string.Equals(current.SecurityStamp, user.SecurityStamp, StringComparison.Ordinal);
            if (!validUser)
            {
                current.RevokedAtUtc = now;
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new UserSessionRotation(false, null, null);
            }

            var nextToken = CreateRefreshToken();
            var next = new UserSession
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RefreshTokenHash = HashRefreshToken(nextToken),
                SecurityStamp = user.SecurityStamp ?? string.Empty,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.Add(lifetime)
            };
            current.RevokedAtUtc = now;
            current.LastUsedAtUtc = now;
            current.ReplacedBySessionId = next.Id;
            _context.UserSessions.Add(next);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new UserSessionRotation(
                true,
                user,
                new UserSessionIssue(next.Id, nextToken, next.ExpiresAtUtc));
        }

        public async Task RevokeSessionAsync(
            Guid sessionId,
            string userId,
            CancellationToken cancellationToken = default)
        {
            var session = await _context.UserSessions.FirstOrDefaultAsync(
                item => item.Id == sessionId && item.UserId == userId && item.RevokedAtUtc == null,
                cancellationToken);
            if (session is null)
                return;

            session.RevokedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task RevokeAllSessionsAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            await _context.UserSessions
                .Where(session => session.UserId == userId && session.RevokedAtUtc == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(session => session.RevokedAtUtc, now),
                    cancellationToken);
        }

        public async Task<IdentityResult> AddToRoleAsync(UserData user, string role, int? companyId = null)
        {
            return await _userManager.AddToRoleAsync(user, role);
        }

        // Management
        public async Task<bool> ToggleUserStatusAsync(string userId, bool isActive)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return false;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            user.IsActive = isActive;
            var result = await _userManager.UpdateSecurityStampAsync(user);
            if (result.Succeeded && user.CompanyId.HasValue)
            {
                await PermissionVersionTracker.StageBumpAsync(_context, user.CompanyId.Value, CancellationToken.None);
            }

            if (result.Succeeded)
            {
                var now = DateTime.UtcNow;
                await _context.UserSessions
                    .Where(session => session.UserId == userId && session.RevokedAtUtc == null)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(
                        session => session.RevokedAtUtc,
                        now));
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            else
            {
                await transaction.RollbackAsync();
            }

            return result.Succeeded;
        }

        public async Task<bool> ToggleCompanyStatusAsync(int companyId, bool isActive)
        {
            var company = await _context.Companies.FindAsync(companyId);
            if (company == null) return false;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            company.IsActive = isActive;
            _context.Companies.Update(company);
            await PermissionVersionTracker.StageBumpAsync(_context, companyId, CancellationToken.None);
            if (!isActive)
            {
                var now = DateTime.UtcNow;
                await _context.UserSessions
                    .Where(session =>
                        session.User.CompanyId == companyId &&
                        session.RevokedAtUtc == null)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(
                        session => session.RevokedAtUtc,
                        now));
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }

        public async Task<List<UserManagementDto>> GetUserManagementAsync(
            int? companyId,
            CancellationToken cancellationToken = default)
        {
            var usersQuery = _context.Users
                .AsNoTracking()
                .AsQueryable();
            if (companyId.HasValue)
                usersQuery = usersQuery.Where(user => user.CompanyId == companyId.Value);

            var users = await usersQuery
                .OrderBy(user => user.UserName)
                .Select(user => new UserManagementDto
                {
                    Id = user.Id,
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    FirstName = user.FirstName ?? string.Empty,
                    LastName = user.LastName ?? string.Empty,
                    PhoneNumber = user.PhoneNumber ?? string.Empty,
                    IsActive = user.IsActive,
                    CompanyId = user.CompanyId,
                    CompanyName = user.Company != null ? user.Company.Name : string.Empty
                })
                .ToListAsync(cancellationToken);

            var rolesQuery =
                from user in _context.Users.AsNoTracking()
                join userRole in _context.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
                join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where role.Name != null
                select new { user.Id, user.CompanyId, Role = role.Name! };
            if (companyId.HasValue)
                rolesQuery = rolesQuery.Where(item => item.CompanyId == companyId.Value);

            var roleRows = await rolesQuery.ToListAsync(cancellationToken);
            var rolesByUser = roleRows.ToLookup(item => item.Id, item => item.Role);
            foreach (var user in users)
                user.Roles = rolesByUser[user.Id].ToList();

            return users;
        }

        public async Task<PagedResult<UserManagementDto>> GetUserManagementPageAsync(
            int? companyId,
            int pageNumber,
            int pageSize,
            string? search,
            CancellationToken cancellationToken = default)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var usersQuery = _context.Users.AsNoTracking().AsQueryable();
            if (companyId.HasValue)
                usersQuery = usersQuery.Where(user => user.CompanyId == companyId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var value = search.Trim();
                usersQuery = usersQuery.Where(user =>
                    (user.UserName != null && user.UserName.Contains(value)) ||
                    (user.Email != null && user.Email.Contains(value)) ||
                    user.FirstName.Contains(value) ||
                    user.LastName.Contains(value));
            }

            var totalCount = await usersQuery.CountAsync(cancellationToken);
            var users = await usersQuery
                .OrderBy(user => user.UserName)
                .ThenBy(user => user.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(user => new UserManagementDto
                {
                    Id = user.Id,
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    FirstName = user.FirstName ?? string.Empty,
                    LastName = user.LastName ?? string.Empty,
                    PhoneNumber = user.PhoneNumber ?? string.Empty,
                    IsActive = user.IsActive,
                    CompanyId = user.CompanyId,
                    CompanyName = user.Company != null ? user.Company.Name : string.Empty
                })
                .ToListAsync(cancellationToken);

            var userIds = users.Select(user => user.Id).ToArray();
            if (userIds.Length > 0)
            {
                var roleRows = await (
                    from userRole in _context.UserRoles.AsNoTracking()
                    join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                    where userIds.Contains(userRole.UserId) && role.Name != null
                    select new { userRole.UserId, Role = role.Name! })
                    .ToListAsync(cancellationToken);
                var rolesByUser = roleRows.ToLookup(item => item.UserId, item => item.Role);
                foreach (var user in users)
                    user.Roles = rolesByUser[user.Id].ToList();
            }

            return new PagedResult<UserManagementDto>(users, totalCount, pageNumber, pageSize);
        }

        private static string CreateRefreshToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static string HashRefreshToken(string refreshToken) =>
            Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(refreshToken)));

        private static bool FixedTimeEquals(string left, string right)
        {
            try
            {
                return CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(left),
                    Convert.FromHexString(right));
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
