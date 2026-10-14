using System.ComponentModel.DataAnnotations;

namespace One.Domain.DTOs.Identity;

public sealed class PasswordResetRequestDto
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(50)]
    public string CompanyCode { get; set; } = string.Empty;
}

public sealed class PasswordResetConfirmDto
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(50)]
    public string CompanyCode { get; set; } = string.Empty;

    [Required, MaxLength(4096)]
    public string Token { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;

    [Required, Compare(nameof(NewPassword))]
    public string ConfirmPassword { get; set; } = string.Empty;
}
