using One.Domain.Base;
using One.Domain.Services.Attributes;

namespace One.Domain.Entities.Sales
{
    [DbTable("SalesInvoices")]
    public class SalesInvoices : BaseEntity<Guid>
    {
        [InsertField(required: true), UpdateField(required: true)]
        public Guid BranchId { get; set; }

        [InsertField(required: true), UpdateField(required: true)]
        public Guid WarehouseId { get; set; }

        [InsertField(required: true), UpdateField(required: true)]
        public Guid CustomerId { get; set; }

        [InsertField(required: true)]
        public string Number { get; set; }

        [InsertField(required: true), UpdateField(required: true)]
        public decimal TotalAmount { get; set; }

        [InsertField(required: true), UpdateField(required: true)]
        public decimal DiscountAmount { get; set; }

        [InsertField(required: true), UpdateField(required: true)]
        public decimal TaxAmount { get; set; }

        [InsertField(required: true), UpdateField(required: true)]
        public decimal NetAmount { get; set; }

        [InsertField(required: true), UpdateField(required: true)]
        public DateTime Date { get; set; }
    }
}