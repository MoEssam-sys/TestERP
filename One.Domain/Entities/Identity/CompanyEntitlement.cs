namespace One.Domain.Entities.Identity
{
    public sealed class CompanyEntitlement
    {
        public long CompanySubscriptionId { get; set; }

        public int CompanyId { get; set; }

        public int AppPageId { get; set; }

        public int PermissionActionId { get; set; }

        public bool IsCustomized { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public CompanySubscription CompanySubscription { get; set; } = null!;

        public AppPageAction AppPageAction { get; set; } = null!;
    }
}
