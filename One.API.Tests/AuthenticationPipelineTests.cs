using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Moq;
using One.Domain.Base;
using One.Domain.DTOs;
using One.Domain.DTOs.Identity;

namespace One.API.Tests;

public sealed class AuthenticationPipelineTests
{
    [Fact]
    public async Task Register_PublicRoleSelection_ReturnsBadRequestWithoutCallingService()
    {
        using var factory = new OneApiFactory();
        using var client = factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync(
            "/api/Authentication/Register?role=SuperAdmin",
            new RegisterUserDto
            {
                UserName = "public-user",
                Password = "ValidPassword1!",
                CompanyId = 1
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        factory.AuthService.Verify(
            service => service.RegisterAsync(
                It.IsAny<RegisterUserDto>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Users_AnonymousRequest_ReturnsUnauthorized()
    {
        using var factory = new OneApiFactory();
        using var client = factory.CreateHttpsClient();

        var response = await client.GetAsync(
            "/api/Users",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnhandledFailure_ReturnsGenericProblemDetails()
    {
        using var factory = new OneApiFactory();
        factory.AuthService.Setup(service => service.LoginAsync(
                It.IsAny<LoginDto>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database password was exposed here"));
        using var client = factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync(
            "/api/Authentication/Login",
            new LoginDto
            {
                Email = "user@one.test",
                Password = "ValidPassword1!"
            },
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("The request could not be completed.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("database password", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("errorId", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestOtp_FourthRequestWithinWindow_ReturnsTooManyRequests()
    {
        using var factory = new OneApiFactory();
        factory.AuthService.Setup(service => service.LoginWithOtpAsync(
                "admin@one.test",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Ok("If the account is eligible, a verification code will be sent."));
        using var client = factory.CreateHttpsClient();
        var request = new OtpRequestDto { Email = "admin@one.test" };

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/Authentication/Login/Otp",
                request,
                TestContext.Current.CancellationToken);
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests],
            statuses);
        factory.AuthService.Verify(
            service => service.LoginWithOtpAsync("admin@one.test", It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        using var factory = new OneApiFactory();
        factory.AuthService.Setup(service => service.LoginAsync(
                It.IsAny<LoginDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthResponseDto
            {
                StatusCode = StatusCodes.Status401Unauthorized,
                Message = "Invalid login attempt."
            });
        using var client = factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync(
            "/api/Authentication/Login",
            new LoginDto
            {
                Email = "user@one.test",
                Password = "wrong-password"
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_EligibleOrMissingAccount_ReturnsSameGenericResponse()
    {
        using var factory = new OneApiFactory();
        factory.AuthService.Setup(service => service.RequestPasswordResetAsync(
                It.IsAny<PasswordResetRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Ok(
                "If the account is eligible, a password reset link will be sent."));
        using var client = factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync(
            "/api/Authentication/Password/Forgot",
            new PasswordResetRequestDto
            {
                Email = "missing@one.test",
                CompanyCode = "CT1"
            },
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("If the account is eligible", body, StringComparison.Ordinal);
        Assert.DoesNotContain("missing", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthLive_RunningProcess_ReturnsHealthy()
    {
        using var factory = new OneApiFactory();
        using var client = factory.CreateHttpsClient();

        var response = await client.GetAsync(
            "/health/live",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
