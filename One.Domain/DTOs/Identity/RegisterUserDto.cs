using System.ComponentModel.DataAnnotations;

namespace One.Domain.DTOs.Identity
{
    public class RegisterUserDto
    {
        [Required]
        public string UserName { get; set; } = "";

        /// <summary>
        /// Email is optional for company users. Only required for SuperAdmin OTP flow.
        /// </summary>
        public string? Email { get; set; }

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = "";

        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public int Age { get; set; }
        public int? CompanyId { get; set; }
    }
}