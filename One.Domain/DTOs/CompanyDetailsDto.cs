namespace One.Domain.DTOs
{
    public class CompanyDetailsDto : CompanySummaryDto
    {
        public List<UserDetailDto> Users { get; set; } = new();
    }

    public class UserDetailDto
    {
        public string Id { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Email { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public List<string> Roles { get; set; } = new();
        public bool IsActive { get; set; } = true; // Assuming active if exists, or add property later
    }
}
