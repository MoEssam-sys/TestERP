namespace One.Domain.Tenancy
{
    public interface ITenantContext
    {
        int CompanyId { get; }

        Guid UserId { get; }
    }

    public sealed class TenantContextUnavailableException(string message)
        : InvalidOperationException(message);
}
