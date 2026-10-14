using System.ComponentModel.DataAnnotations;
using One.Domain.Enums;

namespace One.Domain.Entities.Identity
{
    public sealed class CompanySubscription
    {
        public long Id { get; set; }

        public int CompanyId { get; set; }

        public int SubscriptionPlanId { get; set; }

        public DateTime StartsAtUtc { get; set; }

        public DateTime EndsAtUtc { get; set; }

        public SubscriptionStatus Status { get; set; }

        public decimal PriceSnapshot { get; set; }

        [Required, MaxLength(3)]
        public string CurrencySnapshot { get; set; } = string.Empty;

        public int DurationInDaysSnapshot { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        [Required, MaxLength(450)]
        public string CreatedByUserId { get; set; } = string.Empty;

        public DateTime? UpdatedAtUtc { get; set; }

        [MaxLength(450)]
        public string? UpdatedByUserId { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];

        public Company Company { get; set; } = null!;

        public SubscriptionPlan SubscriptionPlan { get; set; } = null!;

        public ICollection<CompanyEntitlement> Entitlements { get; set; } = [];

        public ICollection<SubscriptionChangeLog> ChangeLogs { get; set; } = [];
    }
}
