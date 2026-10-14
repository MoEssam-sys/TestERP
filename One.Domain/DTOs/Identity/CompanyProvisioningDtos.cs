using System.ComponentModel.DataAnnotations;
using One.Domain.Enums;

namespace One.Domain.DTOs.Identity
{
    public sealed class ProvisionCompanyRequest
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Range(1, int.MaxValue)]
        public int SubscriptionPlanId { get; set; }

        public DateTime? StartsAtUtc { get; set; }

        public IReadOnlyList<PermissionSelectionDto>? Entitlements { get; set; }

        [Required]
        public ProvisionCompanyAdminRequest Admin { get; set; } = new();
    }

    public sealed class ProvisionCompanyAdminRequest
    {
        [Required, MaxLength(256)]
        public string UserName { get; set; } = string.Empty;

        [EmailAddress, MaxLength(256)]
        public string? Email { get; set; }

        [Required, MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Phone, MaxLength(50)]
        public string? PhoneNumber { get; set; }
    }

    public sealed record ProvisionCompanyResultDto(
        int CompanyId,
        long SubscriptionId,
        string AdminUserId,
        DateTime StartsAtUtc,
        DateTime EndsAtUtc,
        SubscriptionStatus SubscriptionStatus,
        int EntitlementCount);
}
