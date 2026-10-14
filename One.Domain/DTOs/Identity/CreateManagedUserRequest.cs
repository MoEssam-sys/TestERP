using System.ComponentModel.DataAnnotations;

namespace One.Domain.DTOs.Identity
{
    public sealed class CreateManagedUserRequest : RegisterUserDto
    {
        [Required]
        public string Role { get; set; } = string.Empty;
    }
}
