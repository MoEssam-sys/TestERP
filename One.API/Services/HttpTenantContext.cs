using System.Security.Claims;
using One.Domain.Tenancy;

namespace One.API.Services
{
    public sealed class HttpTenantContext(IHttpContextAccessor httpContextAccessor)
        : ITenantContext
    {
        public int CompanyId
        {
            get
            {
                var user = httpContextAccessor.HttpContext?.User;
                if (user?.Identity?.IsAuthenticated != true)
                    throw new TenantContextUnavailableException("An authenticated tenant context is required.");

                var rawCompanyId = user.FindFirst("companyId")?.Value;
                if (!int.TryParse(rawCompanyId, out var companyId) || companyId <= 0)
                    throw new TenantContextUnavailableException("A valid company context is required.");

                return companyId;
            }
        }

        public Guid UserId
        {
            get
            {
                var user = httpContextAccessor.HttpContext?.User;
                if (user?.Identity?.IsAuthenticated != true)
                    throw new TenantContextUnavailableException("An authenticated tenant context is required.");

                var rawUserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(rawUserId, out var userId) || userId == Guid.Empty)
                    throw new TenantContextUnavailableException("A valid user context is required.");

                return userId;
            }
        }
    }
}
