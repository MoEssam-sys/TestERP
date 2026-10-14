using One.Domain.Base;
using One.Domain.Services.Attributes;
using System.ComponentModel.DataAnnotations;

namespace One.Domain.DTOs.Stocks
{
    // ------------ Create ------------
    public sealed class CreateWarehouseRequest
    {
        [RequiredGuid]
        public Guid BranchId { get; set; }

        [StringLin(3, 50)]
        public string Code { get; set; }

        [StringLin(3, 300)]
        public string Name { get; set; }

        [Required]
        public bool IsActive { get; set; }
    }

    // ------------ Update ------------
    public sealed class UpdateWarehouseRequest
    {
        [RequiredGuid]
        public Guid Id { get; set; }

        [RequiredGuid]
        public Guid BranchId { get; set; }

        [StringLin(2, 300)]
        public string Name { get; set; }

        [Required]
        public bool IsActive { get; set; }

        public byte[] RowVersion { get; set; } = [];
    }

    // ------------ Activate/Deactivate ------------
    public sealed class ActiveWarehouseRequest
    {
        [RequiredGuid]
        public Guid Id { get; set; }

        [Required]
        public bool IsActive { get; set; }

        public byte[] RowVersion { get; set; } = [];
    }

    // ------------ Filter ------------
    [DbTable("Warehouses")]
    public sealed class FilterWarehouseRequest : BaseFilter
    {
        // LIKE على الاسم
        [MatchOn(Operation.Select, "LIKE", skipIfNull: true)]
        public string? Name { get; set; }

        // فلترة مساوية على الكود (اختياري)
        [MatchOn(Operation.Select, "LIKE", skipIfNull: true)]
        public string? Code { get; set; }

        // Dictionary للأعمدة اللي عايز تعمل عليها IN
        // مثال: { nameof(Companies.Id): [10,11], nameof(Warehouses.Id):[2] }
        [MatchDictionary(Operation.Select, combineWithAnd: true)]
        public Dictionary<string, IEnumerable<object>>? InColumns { get; set; }
    }
}
