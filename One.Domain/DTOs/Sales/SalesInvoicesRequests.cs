using One.Domain.Base;
using One.Domain.Services.Attributes;
using System.ComponentModel.DataAnnotations;

namespace One.Domain.DTOs.Sales
{
    // ------------ Create ------------
    public sealed class CreateSalesInvoiceRequest
    {
        [StringLin(3, 300)]
        public string Number { get; set; }

        [RequiredGuid]
        public Guid BranchId { get; set; }

        [RequiredGuid]
        public Guid WarehouseId { get; set; }

        [RequiredGuid]
        public Guid CustomerId { get; set; }

        [NumberRange(0.01, 99999999.99)]
        public decimal TotalAmount { get; set; }

        [NumberRange(0, 99999999.99)]
        public decimal DiscountAmount { get; set; }

        [NumberRange(0, 99999999.99)]
        public decimal TaxAmount { get; set; }

        [NumberRange(0.01, 99999999.99)]
        public decimal NetAmount { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public bool IsActive { get; set; }

        public ICollection<CreateSalesInvoiceDetailsRequest> SalesInvoiceDetails { get; set; }
    }

    // ------------ Update ------------
    public sealed class UpdateSalesInvoiceRequest
    {
        [RequiredGuid]
        public Guid Id { get; set; }

        [RequiredGuid]
        public Guid BranchId { get; set; }

        [RequiredGuid]
        public Guid WarehouseId { get; set; }

        [RequiredGuid]
        public Guid CustomerId { get; set; }

        [NumberRange(0.01, 99999999.99)]
        public decimal TotalAmount { get; set; }

        [NumberRange(0, 99999999.99)]
        public decimal DiscountAmount { get; set; }

        [NumberRange(0, 99999999.99)]
        public decimal TaxAmount { get; set; }

        [NumberRange(0.01, 99999999.99)]
        public decimal NetAmount { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public bool IsActive { get; set; }

        public ICollection<UpdateSalesInvoiceDetailsRequest> SalesInvoiceDetails { get; set; }
    }

    // ------------ Activate/Deactivate ------------
    public sealed class ActiveSalesInvoiceRequest
    {
        [RequiredGuid]
        public Guid Id { get; set; }

        [Required]
        public bool IsActive { get; set; }

        public string? Column { get; set; }
    }

    // ------------ Filter ------------
    [DbTable("SalesInvoices")]
    public sealed class FilterSalesInvoiceRequest : BaseFilter
    {
        // LIKE على الاسم
        [MatchOn(Operation.Select, "LIKE", skipIfNull: true)]
        public string? Number { get; set; }

        // Add this property to FilterSalesInvoiceRequest for searching by invoice date (exact match)
        [MatchOn(Operation.Select, "=", skipIfNull: true)]
        public DateTime? Date { get; set; }

        [DbColumn("NetAmount")]
        [MatchOn(Operation.Select, "BETWEEN", rangeToProperty: nameof(NetAmountTo), skipIfNull: true)]
        public decimal? NetAmountFrom { get; set; }

        [DbColumn("NetAmount")]
        public decimal? NetAmountTo { get; set; }

        // Dictionary للأعمدة اللي عايز تعمل عليها IN
        // مثال: { nameof(Companies.Id): [10,11], nameof(SalesInvoices.Id):[2] }
        [MatchDictionary(Operation.Select, combineWithAnd: true)]
        public Dictionary<string, IEnumerable<object>>? InColumns { get; set; }
    }
}