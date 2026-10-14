namespace One.Domain.DTOs
{
    public class CompanySummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int UsersCount { get; set; }
        public int AdminsCount { get; set; }
        public DateTime CreatedOn { get; set; }
    }

    public sealed class CompanyLookupDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public sealed class CompanyDashboardDto
    {
        public int TotalCompanies { get; set; }
        public int TotalUsers { get; set; }
        public int TotalAdmins { get; set; }
        public IReadOnlyList<CompanySummaryDto> Companies { get; set; } = [];
    }
}

