namespace One.Domain.Services.Attributes
{
    [DbTable("TestCustomers")]
    public class TestCustomerA
    {
        public int Id { get; set; }

        [DbColumn("TestFullName")]
        [InsertField(required: true), UpdateField(required: true)]
        public string Name { get; set; } = default!;

        [InsertField(required: true)]
        [UpdateField(skipIfNull: true)]
        public string? Mobile { get; set; }

        // العميل A بيحتاج Code في الإضافة
        [InsertField(required: true)]
        public string? Code { get; set; }
    }

    [DbTable("TestCustomers")]
    public class TestCustomerB
    {
        public int Id { get; set; }

        [DbColumn("TestFullName")]
        [InsertField(required: true)]
        [UpdateField(required: true)]
        public string Name { get; set; } = default!;

        [InsertField(required: true)]
        [UpdateField(skipIfNull: true)]
        public string? Mobile { get; set; }

        // العميل B مش محتاج Code في الإضافة => ببساطة مفيش InsertField هنا
        public string? Code { get; set; }
    }

    [DbTable("TestOrders")]
    public class TestOrderKeyOps
    {
        [DeleteField]      // هيتبني WHERE بالحذف عليه
        [ActiveField]      // ونفسه هيتستخدم كمفتاح في التفعيل/التعطيل
        public long OrderId { get; set; }

        // مثال مفتاح إضافي مركّب
        public int BranchId { get; set; }

        // عمود الحالة ممكن يتظبط من الـ Engine بالـ UPDATE (مثلاً IsActive = 1/0)
        public bool IsActive { get; set; }
    }

    [DbTable("TestCustomers")]
    public class CustomerFilter
    {
        // LIKE على الاسم
        [MatchOn(Operation.Select, "LIKE", skipIfNull: true)]
        public string? Name { get; set; }

        // Range على التاريخ: CreatedFrom هو البداية، ومعاه اسم النهاية
        [MatchOn(Operation.Select, "BETWEEN", rangeToProperty: nameof(CreatedTo))]
        public DateTime? CreatedFrom { get; set; }

        public DateTime? CreatedTo { get; set; }

        // Range على التاريخ: CreatedFrom هو البداية، ومعاه اسم النهاية
        [MatchOn(Operation.Select, "BETWEEN", rangeToProperty: nameof(PriceTo))]
        public decimal? PriceFrom { get; set; }

        public decimal? PriceTo { get; set; }

        // فلترة بـ Dictionary: المفتاح = اسم العمود، القيمة = لستة الـ IDs
        // مثال: { "CompanyId": [10,11], "SupplierId": [5] }
        [MatchDictionary(Operation.Select, combineWithAnd: true)]
        public Dictionary<string, IEnumerable<object>>? InColumns { get; set; }
    }
}