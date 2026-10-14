namespace One.Domain.DTOs
{
    public class UserManagementDto
    {
        public string Id { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Email { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public List<string> Roles { get; set; } = new();
        public bool IsActive { get; set; } = true;
        public int? CompanyId { get; set; }
        public string CompanyName { get; set; } = "";
        public DateTime CreatedOn { get; set; }
    }
}
