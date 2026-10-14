using One.Domain.Base;
using One.Domain.Services.Attributes;
using System.ComponentModel.DataAnnotations;

namespace One.Domain.DTOs.Sales
{
    // ------------ Create ------------
    public sealed class CreateBrandRequest
    {
        [StringLin(2, 50)]
        public string Code { get; set; }

        [StringLin(2, 100)]
        public string Name { get; set; }

        public bool IsActive { get; set; }
    }

    // ------------ Update ------------
    public sealed class UpdateBrandRequest
    {
        [RequiredGuid]
        public Guid Id { get; set; }

        [StringLin(2, 100)]
        public string Name { get; set; }

        public bool IsActive { get; set; }

        public byte[] RowVersion { get; set; } = [];
    }

    // ------------ Activate/Deactivate ------------
    public sealed class ActiveBrandRequest
    {
        [RequiredGuid]
        public Guid Id { get; set; }

        public bool IsActive { get; set; }

        public byte[] RowVersion { get; set; } = [];
    }

    // ------------ Filter ------------
    [DbTable("Brands")]
    public sealed class FilterBrandRequest : BaseFilter
    {
        //// LIKE على الاسم
        //[MatchOn(Operation.Select, "LIKE", skipIfNull: true)]
        //public string? Name { get; set; }

        //// فلترة مساوية على الكود (اختياري)
        //[MatchOn(Operation.Select, "LIKE", skipIfNull: true)]
        //public string? Code { get; set; }

        //// Range على تاريخ الإنشاء (جاي من BaseEntity.CreatedAt)
        //[DbColumn("CreatedAt")]
        //[MatchOn(Operation.Select, "BETWEEN", rangeToProperty: nameof(CreatedTo), skipIfNull: true)]
        //public DateTime? CreatedFrom { get; set; }

        //[DbColumn("CreatedAt")]
        //public DateTime? CreatedTo { get; set; }

        // Dictionary للأعمدة اللي عايز تعمل عليها IN
        // مثال: { nameof(Companies.Id): [10,11], nameof(Brands.Id):[2] }
        //[MatchDictionary(Operation.Select, combineWithAnd: true)]
        //public Dictionary<string, IEnumerable<object>>? InColumns { get; set; }
    }
}
