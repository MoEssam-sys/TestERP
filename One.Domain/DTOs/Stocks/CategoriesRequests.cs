using One.Domain.Base;
using One.Domain.Services.Attributes;
using System.ComponentModel.DataAnnotations;

namespace One.Domain.DTOs.Stocks
{
    // ------------ Create ------------
    public sealed class CreateCategoryRequest
    {
        [RequiredGuid]
        public Guid ParentId { get; set; }

        [StringLin(3, 50)]
        public string Code { get; set; }

        [StringLin(3, 300)]
        public string Name { get; set; }

        [Required]
        public bool IsActive { get; set; }
    }

    // ------------ Update ------------
    public sealed class UpdateCategoryRequest
    {
        [RequiredGuid]
        public Guid Id { get; set; }

        [RequiredGuid]
        public Guid ParentId { get; set; }

        [StringLin(2, 300)]
        public string Name { get; set; }

        [Required]
        public bool IsActive { get; set; }
    }

    // ------------ Activate/Deactivate ------------
    public sealed class ActiveCategoryRequest
    {
        [RequiredGuid]
        public Guid Id { get; set; }

        [Required]
        public bool IsActive { get; set; }

        public string? Column { get; set; }
    }

    // ------------ Filter ------------
    [DbTable("Categories")]
    public sealed class FilterCategoryRequest : BaseFilter
    {
        // LIKE على الاسم
        [MatchOn(Operation.Select, "LIKE", skipIfNull: true)]
        public string? Name { get; set; }

        // فلترة مساوية على الكود (اختياري)
        [MatchOn(Operation.Select, "LIKE", skipIfNull: true)]
        public string? Code { get; set; }
    }
}