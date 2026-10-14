using System.ComponentModel.DataAnnotations;

namespace One.Domain.Entities.Identity
{
    public sealed class PermissionAction
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Key { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string NameResourceKey { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<AppPageAction> Pages { get; set; } = [];
    }
}
