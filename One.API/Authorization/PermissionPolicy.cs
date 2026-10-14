using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using One.Infrastructure.Interfaces;

namespace One.API.Authorization
{
    public static class PermissionPolicy
    {
        public const string Prefix = "Permission:";

        public static string Build(string pageKey, string actionKey)
            => $"{Prefix}{pageKey}:{actionKey}";

        public static bool TryParse(string policyName, out string pageKey, out string actionKey)
        {
            pageKey = string.Empty;
            actionKey = string.Empty;
            if (!policyName.StartsWith(Prefix, StringComparison.Ordinal))
                return false;

            var value = policyName[Prefix.Length..];
            var separator = value.LastIndexOf(':');
            if (separator <= 0 || separator == value.Length - 1)
                return false;

            pageKey = value[..separator];
            actionKey = value[(separator + 1)..];
            return true;
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class RequirePermissionAttribute : AuthorizeAttribute
    {
        public RequirePermissionAttribute(string pageKey, string actionKey)
        {
            Policy = PermissionPolicy.Build(pageKey, actionKey);
        }
    }

    public sealed record PermissionRequirement(string PageKey, string ActionKey)
        : IAuthorizationRequirement;

    public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        : DefaultAuthorizationPolicyProvider(options)
    {
        public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            if (!PermissionPolicy.TryParse(policyName, out var pageKey, out var actionKey))
                return base.GetPolicyAsync(policyName);

            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(pageKey, actionKey))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }
    }

    public sealed class PermissionAuthorizationHandler(
        IPermissionService permissionService,
        IHttpContextAccessor httpContextAccessor)
        : AuthorizationHandler<PermissionRequirement>
    {
        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            var cancellationToken = httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
            if (await permissionService.HasPermissionAsync(
                context.User,
                requirement.PageKey,
                requirement.ActionKey,
                cancellationToken))
            {
                context.Succeed(requirement);
            }
        }
    }
}
