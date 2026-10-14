namespace One.Domain.Enums
{
    public enum SubscriptionStatus
    {
        Pending = 0,
        Active = 1,
        Suspended = 2,
        Expired = 3,
        Cancelled = 4
    }

    public enum SubscriptionChangeType
    {
        Created = 0,
        Renewed = 1,
        Suspended = 2,
        Resumed = 3,
        Cancelled = 4,
        Expired = 5,
        Activated = 6,
        EntitlementsChanged = 7
    }
}
