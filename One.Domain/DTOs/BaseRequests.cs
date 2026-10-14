namespace One.Domain.DTOs
{
    public sealed class SearchRequest
    {
        public string Query { get; set; } = "";
        public Guid? FirstId { get; set; }
        public Guid? SecondId { get; set; }

        public string? FirstIdColumn { get; set; }   // مثال: "CompanyId"
        public string? SecondIdColumn { get; set; }  // مثال: "BranchId"

        public bool? IsActive { get; set; } = true;  // اختياري
        public int? MaxResults { get; set; } = 10;   // اختياري
    }
}