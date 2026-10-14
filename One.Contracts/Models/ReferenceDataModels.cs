namespace One.Contracts.Models
{
    public abstract class ReferenceDataModel
    {
        public int CompanyId { get; set; }
        public Guid Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public byte[] RowVersion { get; set; } = [];
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public sealed class BranchModel : ReferenceDataModel
    {
        public string? Address { get; set; }
        public string? Phone { get; set; }
    }

    public sealed class BrandModel : ReferenceDataModel;

    public sealed class WarehouseModel : ReferenceDataModel
    {
        public Guid BranchId { get; set; }
    }
}
