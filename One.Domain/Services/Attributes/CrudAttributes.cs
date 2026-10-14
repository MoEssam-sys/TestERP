using System;
using System.Collections.Generic;

namespace One.Domain.Services.Attributes
{
    /// <summary>
    /// عمليات مدعومة (هتستخدم Select للفلترة).
    /// </summary>
    [Flags]
    public enum Operation
    {
        None = 0,
        Insert = 1 << 0,
        Update = 1 << 1,
        Delete = 1 << 2,
        Select = 1 << 3,
        All = Insert | Update | Delete | Select
    }

    // =========================
    // Class-level
    // =========================

    /// <summary>
    /// تحديد اسم الجدول. لو مش محطوط يبقى اسم الجدول = اسم الكلاس.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class DbTableAttribute : Attribute
    {
        public string? Name { get; }

        public DbTableAttribute(string? name = null) => Name = name;
    }

    /// <summary>
    /// Marks a simple reference-data aggregate that may use BaseRepository.
    /// Transactional and financial aggregates must use dedicated application services.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class ReferenceDataAttribute : Attribute
    {
    }

    // =========================
    // Property-level
    // =========================

    /// <summary>
    /// تغيير اسم العمود في الداتابيز لو مختلف عن اسم الـ property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
    public sealed class DbColumnAttribute : Attribute
    {
        public string? Name { get; }

        public DbColumnAttribute(string? name = null) => Name = name;
    }

    /// <summary>
    /// تضمين الـ property في عملية INSERT.
    /// required=true: الفالديشن لازم يعدّي (مش null، والنص مش فاضي، والـ Guid مش Empty).
    /// قواعد مدى الأرقام تُحدد في DTO أو خدمة المجال حسب معنى الحقل.
    /// skipIfNull=true: لو القيمة null ما تتبعتش في INSERT.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
    public sealed class InsertFieldAttribute : Attribute
    {
        public bool Required { get; }
        public bool SkipIfNull { get; }

        public InsertFieldAttribute(bool required = false, bool skipIfNull = true)
        {
            Required = required;
            SkipIfNull = skipIfNull;
        }
    }

    /// <summary>
    /// تضمين الـ property في عملية UPDATE.
    /// required=true: الفالديشن لازم يعدّي (مش null، والأرقام > 0، والـ Guid مش Empty) — التنفيذ على الراUNTIME.
    /// skipIfNull=true: لو القيمة null ما تتبعتش في SET.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
    public sealed class UpdateFieldAttribute : Attribute
    {
        public bool Required { get; }
        public bool SkipIfNull { get; }

        public UpdateFieldAttribute(bool required = false, bool skipIfNull = true)
        {
            Required = required;
            SkipIfNull = skipIfNull;
        }
    }

    /// <summary>
    /// فلترة (SELECT) لعمود معيّن. Operator زي (=, <>, >, <, LIKE, IN, BETWEEN ...).
    /// skipIfNull=true: تجاهل الشرط لو القيمة null.
    /// rangeToProperty: اسم الـ property المقابل كـ "To" لو ده "From" (أو العكس) علشان RANGES (رقم/تاريخ... إلخ).
    /// مثال: [MatchOn(Operation.Select, "BETWEEN", rangeToProperty: nameof(CreatedTo))]
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
    public sealed class MatchOnAttribute : Attribute
    {
        public Operation AppliesTo { get; }
        public string Operator { get; }
        public bool SkipIfNull { get; }
        public string? RangeToProperty { get; }

        public MatchOnAttribute(
            Operation appliesTo,
            string @operator = "=",
            bool skipIfNull = true,
            string? rangeToProperty = null)
        {
            AppliesTo = appliesTo;
            Operator = @operator;
            SkipIfNull = skipIfNull;
            RangeToProperty = rangeToProperty;
        }
    }

    /// <summary>
    /// فلترة (SELECT) باستخدام Dictionary<string, IEnumerable<object>>
    /// حيث الـ Key = اسم العمود في الجدول، والـ Value = مجموعة القيم للفلترة بـ IN.
    /// combineWithAnd=true: يربط الشروط مع بعض AND؛ لو false هيبقى OR.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
    public sealed class MatchDictionaryAttribute : Attribute
    {
        public Operation AppliesTo { get; }
        public bool CombineWithAnd { get; }
        public string Operator { get; } = "IN";

        public MatchDictionaryAttribute(
            Operation appliesTo,
            bool combineWithAnd = true)
        {
            AppliesTo = appliesTo;
            CombineWithAnd = combineWithAnd;
        }
    }

    /// <summary>
    /// مفاتيح الحذف: أي property عليه DeleteField يدخل في WHERE بتاع DELETE.
    /// لو أكتر من واحد موجودين، كلهم لازم يكونوا Non-Empty وإلا العملية تتلغى (التنفيذ بالراUNTIME).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
    public sealed class DeleteFieldAttribute : Attribute
    {
        public bool RequireNonEmpty { get; }

        public DeleteFieldAttribute(bool requireNonEmpty = true) => RequireNonEmpty = requireNonEmpty;
    }

    /// <summary>
    /// مفاتيح تفعيل/تعطيل (Activate/Deactivate): نفس فكرة DeleteField بالظبط،
    /// أي property عليه ActiveField يدخل في WHERE لتحديد السجل المستهدف.
    /// (تغيير قيمة العمود المسؤول عن الحالة نفسها هيكون من خلال منطق الـ Update/Convention في الـ Engine.)
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
    public sealed class ActiveFieldAttribute : Attribute
    {
        public bool RequireNonEmpty { get; }

        public ActiveFieldAttribute(bool requireNonEmpty = true) => RequireNonEmpty = requireNonEmpty;
    }

    /// <summary>Original database row version used for optimistic concurrency.</summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class ConcurrencyFieldAttribute : Attribute
    {
    }
}
