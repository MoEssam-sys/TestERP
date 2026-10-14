using System.ComponentModel.DataAnnotations;

namespace One.Domain.Entities.Identity
{
    public sealed class SubscriptionPlan
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        public decimal Price { get; set; }

        [Required, MaxLength(3)]
        public string Currency { get; set; } = string.Empty;

        public int DurationInDays { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; }

        [Required, MaxLength(450)]
        public string CreatedByUserId { get; set; } = string.Empty;

        public DateTime? UpdatedAtUtc { get; set; }

        [MaxLength(450)]
        public string? UpdatedByUserId { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];

        public ICollection<PlanPermission> Permissions { get; set; } = [];

        public ICollection<CompanySubscription> CompanySubscriptions { get; set; } = [];
    }
}
