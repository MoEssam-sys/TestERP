using One.Domain.Base;
using One.Domain.Services.Attributes;

namespace One.Domain.Entities.Stocks
{
    [DbTable("Categories")]
    public class Categories : BaseEntity<Guid>
    {
        [InsertField(required: true), UpdateField(required: true)]
        public Guid ParentId { get; set; }

        [InsertField(required: true)]
        public string Code { get; set; }

        [InsertField(required: true), UpdateField(required: true)]
        public string Name { get; set; }

        // هل السجل مفعّل؟
        [InsertField(required: true), UpdateField(required: true)]
        public bool IsActive { get; set; } = true;
    }
}