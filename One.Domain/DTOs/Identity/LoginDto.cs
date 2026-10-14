namespace One.Domain.DTOs.Identity
{
    public class LoginDto
    {
        public string Email { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Password { get; set; } = "";
        public string CompanyCode { get; set; } = "";
    }
}
