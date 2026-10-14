using One.Domain.Base;
using One.Domain.Enums;

namespace One.Application.Services.AdvNavigation
{
    public static class Filter
    {
        public static FilterDescriptor Eq(string prop, object value)
            => new() { Property = prop, Type = FilterType.Equals, Value = value };

        public static FilterDescriptor Contains(string prop, object value)
            => new() { Property = prop, Type = FilterType.Contains, Value = value };

        public static FilterDescriptor Range(string prop, object from, object to)
            => new() { Property = prop, Type = FilterType.Range, Value = from, ValueTo = to };

        public static FilterDescriptor Gt(string prop, object value)
            => new() { Property = prop, Type = FilterType.GreaterThan, Value = value };

        public static FilterDescriptor Lt(string prop, object value)
            => new() { Property = prop, Type = FilterType.LessThan, Value = value };

        // زود أكتر لو حبيت
    }
}