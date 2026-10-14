using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Security.Claims;
using One.Domain.Base;
using One.Domain.DTOs.Identity;
using One.Shared.Components;
using One.Shared.Components.Pages;
using One.Shared.Configuration;
using One.Shared.Services;
using One.Shared.Services.Api;
using One.Shared.Services.Logging;
using One.Shared.Services.Platform;
using One.Shared.Services.Session;

namespace One.Shared.Tests;

public sealed class PermissionViewTests
{
    [Fact]
    public async Task Render_GrantedAction_ShowsChildContent()
    {
        var api = new Mock<IOneApiClient>(MockBehavior.Strict);
        api.Setup(item => item.GetAsync<EffectivePermissionsDto>(
                "api/permissions/effective",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EffectivePermissionsDto>.Ok(Permissions(
                new PermissionSelectionDto("Sales.Brands", "Create"))));
        var state = new EffectivePermissionState(api.Object);
        Assert.True(await state.RefreshAsync(cancellationToken: Xunit.TestContext.Current.CancellationToken));
        await using var context = new BunitContext();
        ConfigureServices(context, api.Object, state);

        var component = context.Render<PermissionView>(parameters => parameters
            .Add(item => item.PageKey, "Sales.Brands")
            .Add(item => item.ActionKey, "Create")
            .AddChildContent("<span data-test='allowed'>Allowed</span>"));

        Assert.Contains("data-test='allowed'", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_MissingAction_HidesChildContent()
    {
        var api = new Mock<IOneApiClient>(MockBehavior.Strict);
        var state = new EffectivePermissionState(api.Object);
        await using var context = new BunitContext();
        ConfigureServices(context, api.Object, state);

        var component = context.Render<PermissionView>(parameters => parameters
            .Add(item => item.PageKey, "Sales.Brands")
            .Add(item => item.ActionKey, "Delete")
            .AddChildContent("<span data-test='denied'>Denied</span>"));

        Assert.DoesNotContain("data-test", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_AnonymousProtectedRoute_DoesNotRequestPermissions()
    {
        var api = new Mock<IOneApiClient>(MockBehavior.Strict);
        var session = new Mock<IClientSession>(MockBehavior.Strict);
        session.SetupGet(item => item.IsAuthenticated).Returns(false);
        session.SetupGet(item => item.User).Returns(new ClaimsPrincipal(new ClaimsIdentity()));
        session.Setup(item => item.InitializeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var state = new EffectivePermissionState(api.Object);
        await using var context = new BunitContext();
        ConfigureServices(context, api.Object, state, session.Object);

        context.Render<PermissionRouteView>(parameters => parameters
            .Add(item => item.RouteData, new RouteData(typeof(Home), new Dictionary<string, object?>())));

        session.Verify(item => item.InitializeAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        api.VerifyNoOtherCalls();
    }

    private static EffectivePermissionsDto Permissions(params PermissionSelectionDto[] permissions) =>
        new(1, false, 1, null, null, permissions, []);

    private static void ConfigureServices(
        BunitContext context,
        IOneApiClient apiClient,
        EffectivePermissionState permissionState,
        IClientSession? session = null)
    {
        context.Services.AddOneShared();
        context.Services.AddSingleton(apiClient);
        context.Services.AddSingleton(permissionState);
        if (session is not null)
        {
            context.Services.AddSingleton(session);
        }

        context.Services.AddSingleton(Mock.Of<IClientStorage>());
        context.Services.AddSingleton(Mock.Of<IClientLogger>());
    }
}
