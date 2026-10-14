using System.ComponentModel.DataAnnotations;
using One.Domain.Enums;

namespace One.Domain.Entities.Identity
{
    public sealed class SubscriptionChangeLog
    {
        public long Id { get; set; }

        public long CompanySubscriptionId { get; set; }

        public int CompanyId { get; set; }

        public SubscriptionChangeType ChangeType { get; set; }

        public SubscriptionStatus? PreviousStatus { get; set; }

        public SubscriptionStatus NewStatus { get; set; }

        [MaxLength(2000)]
        public string? Details { get; set; }

        public DateTime OccurredAtUtc { get; set; }

        [MaxLength(450)]
        public string? PerformedByUserId { get; set; }

        public CompanySubscription CompanySubscription { get; set; } = null!;
    }
}
