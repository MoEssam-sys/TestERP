using One.Domain.Base;
using One.Domain.Services.Attributes;

namespace One.Domain.Entities.Sales
{
    [DbTable("SalesInvoiceDetails")]
    public class SalesInvoiceDetails : BaseEntity<Guid>
    {
        [InsertField(required: true)]
        public Guid InvoiceId { get; set; }

        [InsertField(required: true), UpdateField(required: true)]
        public Guid ProductId { get; set; }

        [InsertField(required: true), UpdateField(required: true)]
        public decimal Quantity { get; set; }

        [InsertField(required: true), UpdateField(required: true)]
        public decimal UnitPrice { get; set; }

        [InsertField(required: true), UpdateField(required: true)]
        public decimal Discount { get; set; }

        [InsertField(required: true), UpdateField(required: true)]
        public decimal Tax { get; set; }

        public decimal LineTotal { get; set; }
    }
}