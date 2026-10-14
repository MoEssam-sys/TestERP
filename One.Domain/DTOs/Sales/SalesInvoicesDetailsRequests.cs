using One.Domain.Services.Attributes;

namespace One.Domain.DTOs.Sales
{
    // ------------ Create ------------
    public sealed class CreateSalesInvoiceDetailsRequest
    {
        [RequiredGuid]
        public Guid ProductId { get; set; }

        [NumberRange(0.01, 99999999.99)]
        public decimal Quantity { get; set; }

        [NumberRange(0, 99999999.99)]
        public decimal UnitPrice { get; set; }

        [NumberRange(0, 99999999.99)]
        public decimal Discount { get; set; }

        [NumberRange(0, 99999999.99)]
        public decimal Tax { get; set; }
    }

    // ------------ Update ------------
    public sealed class UpdateSalesInvoiceDetailsRequest
    {
        [RequiredGuid]
        public Guid Id { get; set; }

        [RequiredGuid]
        public Guid ProductId { get; set; }

        [NumberRange(0.01, 99999999.99)]
        public decimal Quantity { get; set; }

        [NumberRange(0, 99999999.99)]
        public decimal UnitPrice { get; set; }

        [NumberRange(0, 99999999.99)]
        public decimal Discount { get; set; }

        [NumberRange(0, 99999999.99)]
        public decimal Tax { get; set; }
    }
}