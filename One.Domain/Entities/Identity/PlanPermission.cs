namespace One.Domain.Entities.Identity
{
    public sealed class PlanPermission
    {
        public int SubscriptionPlanId { get; set; }

        public int AppPageId { get; set; }

        public int PermissionActionId { get; set; }

        public SubscriptionPlan SubscriptionPlan { get; set; } = null!;

        public AppPageAction AppPageAction { get; set; } = null!;
    }
}
