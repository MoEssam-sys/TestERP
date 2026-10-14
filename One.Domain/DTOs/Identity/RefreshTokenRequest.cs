using System.ComponentModel.DataAnnotations;

namespace One.Domain.DTOs.Identity;

public sealed class RefreshTokenRequest
{
    [Required]
    [MaxLength(256)]
    public string RefreshToken { get; set; } = string.Empty;
}
