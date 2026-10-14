using One.Domain.Base;
using One.Domain.Services.Attributes;

namespace One.Domain.Entities.Sales
{
    [DbTable("ProductUnits")]
    public class ProductUnits : BaseEntity<Guid>
    {
        [InsertField(required: true), UpdateField(required: true)]
        public Guid ProductId { get; set; }

        [InsertField(required: true), UpdateField(required: true)]
        public Guid UnitId { get; set; }

        [InsertField(required: true), UpdateField(required: true)]
        public decimal ConversionRate { get; set; }

        [InsertField(required: true), UpdateField(required: true)]
        public decimal SalePrice { get; set; }

        [InsertField(required: true), UpdateField(required: true)]
        public decimal PurchasePrice { get; set; }

        [InsertField(required: true), UpdateField(required: true)]
        public bool IsDefault { get; set; } = true;

        [InsertField(required: true), UpdateField(required: true)]
        public bool IsActive { get; set; } = true;
    }
}