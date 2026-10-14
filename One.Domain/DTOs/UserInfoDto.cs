namespace One.Domain.DTOs
{
    public class UserInfoDto
    {
        public string UserName { get; set; } = "";
        public string Email { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public int Age { get; set; }
    }
}