using One.Domain.Base;
using One.Domain.Services.Attributes;

namespace One.Domain.Entities.Stocks
{
    [DbTable("Warehouses")]
    [ReferenceData]
    public class Warehouses : BaseEntity<Guid>
    {
        [InsertField(required: true), UpdateField(required: true)]
        public Guid BranchId { get; set; }

        [InsertField(required: true)]
        public string Code { get; set; }

        [InsertField(required: true), UpdateField(required: true)]
        public string Name { get; set; }

        // هل السجل مفعّل؟
        [InsertField(required: true)]
        public bool IsActive { get; set; } = true;
    }
}
