using System;
using System.ComponentModel.DataAnnotations;

namespace One.Domain.Entities.Identity
{
    public class OtpChallenge
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string OtpHash { get; set; } = string.Empty;

        public DateTime ExpiresAtUtc { get; set; }

        public int Attempts { get; set; }

        public DateTime? UsedAtUtc { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime LastSentAtUtc { get; set; } = DateTime.UtcNow;
    }
}
