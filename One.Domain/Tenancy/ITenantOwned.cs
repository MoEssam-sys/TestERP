namespace One.Domain.Tenancy
{
    public interface ITenantOwned
    {
        int CompanyId { get; set; }
    }
}
