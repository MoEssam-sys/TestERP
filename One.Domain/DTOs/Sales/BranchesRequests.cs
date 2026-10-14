using One.Domain.Base;
using One.Domain.Services.Attributes;
using System.ComponentModel.DataAnnotations;

namespace One.Domain.DTOs.Sales
{
    // ------------ Create ------------
    public sealed class CreateBranchRequest
    {
        [StringLin(3, 50)]
        public string Code { get; set; }

        [StringLin(3, 300)]
        public string Name { get; set; }

        [StringLin(0, 1000)]
        public string? Address { get; set; }

        [StringLin(0, 20)]
        public string? Phone { get; set; }

        [Required]
        public bool IsActive { get; set; }
    }

    // ------------ Update ------------
    public sealed class UpdateBranchRequest
    {
        [RequiredGuid]
        public Guid Id { get; set; }

        [StringLin(2, 300)]
        public string Name { get; set; }

        [StringLin(0, 1000)]
        public string? Address { get; set; }

        [StringLin(0, 20)]
        public string? Phone { get; set; }

        [Required]
        public bool IsActive { get; set; }

        public byte[] RowVersion { get; set; } = [];
    }

    // ------------ Activate/Deactivate ------------
    public sealed class ActiveBranchRequest
    {
        [RequiredGuid]
        public Guid Id { get; set; }

        [Required]
        public bool IsActive { get; set; }

        public byte[] RowVersion { get; set; } = [];
    }

    // ------------ Filter ------------
    [DbTable("Branches")]
    public sealed class FilterBranchRequest : BaseFilter
    {
        //// LIKE على الاسم
        //[MatchOn(Operation.Select, "LIKE", skipIfNull: true)]
        //public string? Name { get; set; }

        //// فلترة مساوية على الكود (اختياري)
        //[MatchOn(Operation.Select, "LIKE", skipIfNull: true)]
        //public string? Code { get; set; }

        //[MatchOn(Operation.Select, "LIKE", skipIfNull: true)]
        //public string? Phone { get; set; }

        //// Range على تاريخ الإنشاء (جاي من BaseEntity.CreatedAt)
        //[DbColumn("CreatedAt")]
        //[MatchOn(Operation.Select, "BETWEEN", rangeToProperty: nameof(CreatedTo), skipIfNull: true)]
        //public DateTime? CreatedFrom { get; set; }

        //[DbColumn("CreatedAt")]
        //public DateTime? CreatedTo { get; set; }

        // Dictionary للأعمدة اللي عايز تعمل عليها IN
        // مثال: { nameof(Companies.Id): [10,11], nameof(Branchs.Id):[2] }
        //[MatchDictionary(Operation.Select, combineWithAnd: true)]
        //public Dictionary<string, IEnumerable<object>>? InColumns { get; set; }
    }
}
