namespace One.Domain.DTOs.Identity
{
    public class CompanyValidationDto
    {
        public int CompanyId { get; set; }
        public string Name { get; set; } = "";
        public bool RequiresAdminRegistration { get; set; }
    }
}
