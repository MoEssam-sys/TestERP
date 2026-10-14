using One.Domain.Services.Attributes;
using System.ComponentModel.DataAnnotations;

namespace One.Domain.DTOs.Sales
{
    // ------------ Create ------------
    public sealed class CreateProductUnitRequest
    {
        [RequiredGuid]
        public Guid UnitId { get; set; }

        [NumberRange(0.01, 99999999.9999)]
        public decimal ConversionRate { get; set; }

        [NumberRange(0.01, 99999999.99)]
        public decimal SalePrice { get; set; }

        [NumberRange(0.01, 99999999.9999)]
        public decimal PurchasePrice { get; set; }

        [Required]
        public bool IsDefault { get; set; }

        [Required]
        public bool IsActive { get; set; }
    }

    // ------------ Update ------------
    public sealed class UpdateProductUnitRequest
    {
        [RequiredGuid]
        public Guid Id { get; set; }

        [RequiredGuid]
        public Guid UnitId { get; set; }

        [NumberRange(0.01, 99999999.9999)]
        public decimal ConversionRate { get; set; }

        [NumberRange(0.01, 99999999.99)]
        public decimal SalePrice { get; set; }

        [NumberRange(0.01, 99999999.9999)]
        public decimal PurchasePrice { get; set; }

        [Required]
        public bool IsDefault { get; set; }

        [Required]
        public bool IsActive { get; set; }
    }

    // ------------ Activate/Deactivate ------------
    public sealed class ActiveProductUnitRequest
    {
        [RequiredGuid]
        public Guid Id { get; set; }

        [Required]
        public bool IsActive { get; set; }

        public string? Column { get; set; }
    }
}