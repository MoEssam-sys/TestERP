using One.Domain.Base;
using One.Domain.Services.Attributes;
using System.ComponentModel.DataAnnotations;

namespace One.Domain.DTOs.Sales
{
    // ------------ Create ------------
    public sealed class CreateProductRequest
    {
        [RequiredGuid]
        public Guid CategoryId { get; set; }

        [StringLin(3, 50)]
        public string Code { get; set; }

        [StringLin(3, 300)]
        public string Name { get; set; }

        [Required]
        public bool IsActive { get; set; }

        public ICollection<CreateProductUnitRequest> ProductUnits { get; set; }
    }

    // ------------ Update ------------
    public sealed class UpdateProductRequest
    {
        [RequiredGuid]
        public Guid Id { get; set; }

        [RequiredGuid]
        public Guid CategoryId { get; set; }

        [StringLin(2, 300)]
        public string Name { get; set; }

        [Required]
        public bool IsActive { get; set; }

        public ICollection<UpdateProductUnitRequest> ProductUnits { get; set; }
    }

    // ------------ Activate/Deactivate ------------
    public sealed class ActiveProductRequest
    {
        [RequiredGuid]
        public Guid Id { get; set; }

        [Required]
        public bool IsActive { get; set; }

        public string? Column { get; set; }
    }

    // ------------ Filter ------------
    [DbTable("Products")]
    public sealed class FilterProductRequest : BaseFilter
    {
        // LIKE على الاسم
        [MatchOn(Operation.Select, "LIKE", skipIfNull: true)]
        public string? Name { get; set; }

        // فلترة مساوية على الكود (اختياري)
        [MatchOn(Operation.Select, "LIKE", skipIfNull: true)]
        public string? Code { get; set; }

        [DbColumn("SalePrice")]
        [MatchOn(Operation.Select, "BETWEEN", rangeToProperty: nameof(SalePriceTo), skipIfNull: true)]
        public DateTime? SalePriceFrom { get; set; }

        [DbColumn("SalePrice")]
        public DateTime? SalePriceTo { get; set; }

        [DbColumn("PurchasePrice")]
        [MatchOn(Operation.Select, "BETWEEN", rangeToProperty: nameof(PurchasePriceTo), skipIfNull: true)]
        public DateTime? PurchasePriceFrom { get; set; }

        [DbColumn("PurchasePrice")]
        public DateTime? PurchasePriceTo { get; set; }

        // Dictionary للأعمدة اللي عايز تعمل عليها IN
        // مثال: { nameof(Companies.Id): [10,11], nameof(Products.Id):[2] }
        [MatchDictionary(Operation.Select, combineWithAnd: true)]
        public Dictionary<string, IEnumerable<object>>? InColumns { get; set; }
    }
}