using One.Domain.Base;
using One.Domain.Services.Attributes;

namespace One.Domain.Entities.Sales
{
    [DbTable("Branches")]
    [ReferenceData]
    public class Branches : BaseEntity<Guid>
    {
        [InsertField(required: true)]
        public string Code { get; set; }

        [InsertField(required: true), UpdateField(required: true)]
        public string Name { get; set; }

        [InsertField, UpdateField(skipIfNull: false)]
        public string? Address { get; set; }

        [InsertField, UpdateField(skipIfNull: false)]
        public string? Phone { get; set; }

        // هل السجل مفعّل؟
        [InsertField(required: true)]
        public bool IsActive { get; set; } = true;
    }
}
