namespace One.Domain.Entities.Identity
{
    public sealed class CompanyPermissionVersion
    {
        public int CompanyId { get; set; }

        public long Version { get; set; } = 1;

        public DateTime UpdatedAtUtc { get; set; }

        public Company Company { get; set; } = null!;
    }
}
