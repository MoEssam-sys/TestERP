using System.ComponentModel.DataAnnotations;

namespace One.Domain.Entities.Identity
{
    public sealed class UserPermission
    {
        public long Id { get; set; }

        public int CompanyId { get; set; }

        [Required, MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        public int AppPageId { get; set; }

        public int PermissionActionId { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        [Required, MaxLength(450)]
        public string GrantedByUserId { get; set; } = string.Empty;

        public Company Company { get; set; } = null!;

        public UserData User { get; set; } = null!;

        public AppPageAction AppPageAction { get; set; } = null!;
    }
}
