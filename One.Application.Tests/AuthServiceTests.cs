using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using One.Application.Services;
using One.Domain.Constants;
using One.Domain.DTOs.Identity;
using One.Domain.Entities.Identity;
using One.Infrastructure.Interfaces;

namespace One.Application.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_InvalidPassword_RecordsFailureAndReturnsUnauthorized()
    {
        var user = ActiveUser(companyId: 1);
        var repository = new Mock<IAuthRepository>(MockBehavior.Strict);
        repository.Setup(item => item.FindByEmailAsync("user@one.test")).ReturnsAsync(user);
        repository.Setup(item => item.IsLockedOutAsync(user)).ReturnsAsync(false);
        repository.Setup(item => item.GetCompanyByIdAsync(1)).ReturnsAsync(ActiveCompany(1, "CT1"));
        repository.Setup(item => item.CheckPasswordAsync(user, "wrong-password")).ReturnsAsync(false);
        repository.Setup(item => item.AccessFailedAsync(user)).Returns(Task.CompletedTask);

        var result = await CreateService(repository).LoginAsync(
            new LoginDto
            {
                Email = "user@one.test",
                Password = "wrong-password",
                CompanyCode = "CT1"
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("Invalid login attempt.", result.Message);
        repository.Verify(item => item.AccessFailedAsync(user), Times.Once);
        repository.VerifyAll();
    }

    [Fact]
    public async Task LoginAsync_InactiveCompany_DoesNotIssueSession()
    {
        var user = ActiveUser(companyId: 7);
        var repository = new Mock<IAuthRepository>(MockBehavior.Strict);
        repository.Setup(item => item.FindByEmailAsync("user@one.test")).ReturnsAsync(user);
        repository.Setup(item => item.IsLockedOutAsync(user)).ReturnsAsync(false);
        repository.Setup(item => item.GetCompanyByIdAsync(7)).ReturnsAsync(new Company
        {
            Id = 7,
            Code = "CT7",
            Name = "Inactive tenant",
            IsActive = false
        });

        var result = await CreateService(repository).LoginAsync(
            new LoginDto
            {
                Email = "user@one.test",
                Password = "correct-password",
                CompanyCode = "CT7"
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        repository.Verify(
            item => item.CreateSessionAsync(
                It.IsAny<UserData>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        repository.VerifyAll();
    }

    [Fact]
    public async Task ToggleUserStatusAsync_CompanyAdminTargetsOtherTenant_ReturnsForbidden()
    {
        var target = ActiveUser(companyId: 2);
        var repository = new Mock<IAuthRepository>(MockBehavior.Strict);
        repository.Setup(item => item.GetUserByIdAsync(target.Id)).ReturnsAsync(target);
        var actor = Principal(target.Id, 1, SystemRoles.CompanyAdmin);

        var result = await CreateService(repository).ToggleUserStatusAsync(
            target.Id,
            false,
            actor,
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        repository.Verify(item => item.ToggleUserStatusAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        repository.VerifyAll();
    }

    [Fact]
    public async Task LoginWithOtpAsync_UnknownEmail_ReturnsGenericResponseWithoutSendingEmail()
    {
        var repository = new Mock<IAuthRepository>(MockBehavior.Strict);
        repository.Setup(item => item.FindByEmailAsync("missing@one.test")).ReturnsAsync((UserData?)null);
        var email = new Mock<IEmailService>(MockBehavior.Strict);
        var service = CreateService(repository, email);

        var result = await service.LoginWithOtpAsync(
            "missing@one.test",
            TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("If the account is eligible, a verification code will be sent.", result.Data);
        email.Verify(
            item => item.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        repository.VerifyAll();
    }

    [Fact]
    public async Task RefreshSessionAsync_ReplayedToken_ReturnsUnauthorized()
    {
        var repository = new Mock<IAuthRepository>(MockBehavior.Strict);
        repository.Setup(item => item.RotateSessionAsync(
                "replayed-token",
                TimeSpan.FromDays(14),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSessionRotation(false, null, null));

        var result = await CreateService(repository).RefreshSessionAsync(
            new RefreshTokenRequest { RefreshToken = "replayed-token" },
            TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        repository.VerifyAll();
    }

    [Fact]
    public async Task LoginAsync_WrongCompanyCode_DoesNotCheckPasswordOrIssueSession()
    {
        var user = ActiveUser(companyId: 1);
        var repository = new Mock<IAuthRepository>(MockBehavior.Strict);
        repository.Setup(item => item.FindByEmailAsync("user@one.test")).ReturnsAsync(user);
        repository.Setup(item => item.IsLockedOutAsync(user)).ReturnsAsync(false);
        repository.Setup(item => item.GetCompanyByIdAsync(1)).ReturnsAsync(ActiveCompany(1, "CT1"));

        var result = await CreateService(repository).LoginAsync(
            new LoginDto
            {
                Email = "user@one.test",
                Password = "correct-password",
                CompanyCode = "CT2"
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        repository.Verify(
            item => item.CheckPasswordAsync(It.IsAny<UserData>(), It.IsAny<string>()),
            Times.Never);
        repository.Verify(
            item => item.CreateSessionAsync(
                It.IsAny<UserData>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        repository.VerifyAll();
    }

    [Fact]
    public async Task RequestPasswordResetAsync_UnknownEmail_ReturnsGenericResponseWithoutSendingEmail()
    {
        var repository = new Mock<IAuthRepository>(MockBehavior.Strict);
        repository.Setup(item => item.FindByEmailAsync("missing@one.test")).ReturnsAsync((UserData?)null);
        var email = new Mock<IEmailService>(MockBehavior.Strict);

        var result = await CreateService(repository, email).RequestPasswordResetAsync(
            new PasswordResetRequestDto { Email = "missing@one.test", CompanyCode = "CT1" },
            TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(
            "If the account is eligible, a password reset link will be sent.",
            result.Data);
        email.Verify(
            item => item.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        repository.VerifyAll();
    }

    [Fact]
    public async Task RequestPasswordResetAsync_ActiveCompanyUser_SendsScopedResetLink()
    {
        var user = ActiveUser(companyId: 1);
        var repository = new Mock<IAuthRepository>(MockBehavior.Strict);
        repository.Setup(item => item.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        repository.Setup(item => item.GetCompanyByIdAsync(1)).ReturnsAsync(ActiveCompany(1, "CT1"));
        repository.Setup(item => item.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("identity-token");
        var email = new Mock<IEmailService>(MockBehavior.Strict);
        email.Setup(item => item.SendEmailAsync(
                user.Email!,
                It.IsAny<string>(),
                It.Is<string>(body =>
                    body.Contains("reset-password?email=", StringComparison.Ordinal) &&
                    body.Contains("company=CT1", StringComparison.Ordinal))))
            .Returns(Task.CompletedTask);

        var result = await CreateService(repository, email).RequestPasswordResetAsync(
            new PasswordResetRequestDto { Email = user.Email!, CompanyCode = "CT1" },
            TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        repository.VerifyAll();
        email.VerifyAll();
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidToken_RevokesExistingSessions()
    {
        var user = ActiveUser(companyId: 1);
        var repository = new Mock<IAuthRepository>(MockBehavior.Strict);
        repository.Setup(item => item.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        repository.Setup(item => item.GetCompanyByIdAsync(1)).ReturnsAsync(ActiveCompany(1, "CT1"));
        repository.Setup(item => item.ResetPasswordAsync(
                user,
                "identity-token",
                "NewPassword1!",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);
        repository.Setup(item => item.RevokeAllSessionsAsync(
                user.Id,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateService(repository).ResetPasswordAsync(
            new PasswordResetConfirmDto
            {
                Email = user.Email!,
                CompanyCode = "CT1",
                Token = "aWRlbnRpdHktdG9rZW4",
                NewPassword = "NewPassword1!",
                ConfirmPassword = "NewPassword1!"
            },
            TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        repository.VerifyAll();
    }

    private static AuthService CreateService(
        Mock<IAuthRepository> repository,
        Mock<IEmailService>? email = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["JWT:Secret"] = new string('J', 64),
            ["JWT:ValidIssuer"] = "OneERP.Tests",
            ["JWT:ValidAudience"] = "OneERP.Tests",
            ["Security:OtpPepper"] = new string('O', 64),
            ["Authentication:AccessTokenMinutes"] = "30",
            ["Authentication:RefreshTokenDays"] = "14",
            ["Authentication:Otp:CodeLength"] = "6",
            ["Authentication:Otp:LifetimeMinutes"] = "5",
            ["Authentication:Otp:ResendCooldownSeconds"] = "60",
            ["Authentication:Otp:MaxAttempts"] = "5",
            ["Authentication:PasswordReset:ClientBaseUrl"] = "https://app.one.test",
            ["Authentication:PasswordReset:TokenMinutes"] = "20"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return new AuthService(
            configuration,
            repository.Object,
            (email ?? new Mock<IEmailService>(MockBehavior.Strict)).Object,
            NullLogger<AuthService>.Instance);
    }

    private static UserData ActiveUser(int companyId) => new()
    {
        Id = Guid.NewGuid().ToString(),
        UserName = "one-user",
        Email = "user@one.test",
        FirstName = "One",
        LastName = "User",
        CompanyId = companyId,
        IsActive = true,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    private static Company ActiveCompany(int id, string code) => new()
    {
        Id = id,
        Code = code,
        Name = $"Company {code}",
        IsActive = true
    };

    private static ClaimsPrincipal Principal(string userId, int companyId, string role) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("companyId", companyId.ToString()),
            new Claim(ClaimTypes.Role, role)
        ],
        "Test",
        ClaimTypes.Name,
        ClaimTypes.Role));
}
