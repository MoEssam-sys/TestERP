using System.ComponentModel.DataAnnotations;

namespace One.Domain.Entities.Identity
{
    public class Company
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        // Navigation property
        public virtual ICollection<UserData> Users { get; set; } = new List<UserData>();
    }
}

