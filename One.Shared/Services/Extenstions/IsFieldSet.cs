using System.Collections;

namespace One.Shared.Services.Extenstions
{
    public static class IsFieldSet
    {
        // String
        public static bool IsNullOrDef(this string value)
            => string.IsNullOrWhiteSpace(value);

        public static bool IsNotNullOrDef(this string value)
            => !string.IsNullOrWhiteSpace(value);

        // Nullable int
        public static bool IsNullOrDef(this int? value)
            => !value.HasValue || value.Value == 0;

        public static bool IsNotNullOrDef(this int? value)
            => value.HasValue && value.Value != 0;

        // int
        public static bool IsNullOrDef(this int value)
            => value == 0;

        public static bool IsNotNullOrDef(this int value)
            => value != 0;

        // Nullable double
        public static bool IsNullOrDef(this double? value)
            => !value.HasValue || value.Value == 0;

        public static bool IsNotNullOrDef(this double? value)
            => value.HasValue && value.Value != 0;

        // double
        public static bool IsNullOrDef(this double value)
            => value == 0;

        public static bool IsNotNullOrDef(this double value)
            => value != 0;

        // Nullable float
        public static bool IsNullOrDef(this float? value)
            => !value.HasValue || value.Value == 0;

        public static bool IsNotNullOrDef(this float? value)
            => value.HasValue && value.Value != 0;

        // float
        public static bool IsNullOrDef(this float value)
            => value == 0;

        public static bool IsNotNullOrDef(this float value)
            => value != 0;

        // Nullable decimal
        public static bool IsNullOrDef(this decimal? value)
            => !value.HasValue || value.Value == 0;

        public static bool IsNotNullOrDef(this decimal? value)
            => value.HasValue && value.Value != 0;

        // decimal
        public static bool IsNullOrDef(this decimal value)
            => value == 0;

        public static bool IsNotNullOrDef(this decimal value)
            => value != 0;

        // Nullable long
        public static bool IsNullOrDef(this long? value)
            => !value.HasValue || value.Value == 0;

        public static bool IsNotNullOrDef(this long? value)
            => value.HasValue && value.Value != 0;

        // long
        public static bool IsNullOrDef(this long value)
            => value == 0;

        public static bool IsNotNullOrDef(this long value)
            => value != 0;

        // Nullable short
        public static bool IsNullOrDef(this short? value)
            => !value.HasValue || value.Value == 0;

        public static bool IsNotNullOrDef(this short? value)
            => value.HasValue && value.Value != 0;

        // short
        public static bool IsNullOrDef(this short value)
            => value == 0;

        public static bool IsNotNullOrDef(this short value)
            => value != 0;

        // Nullable byte
        public static bool IsNullOrDef(this byte? value)
            => !value.HasValue || value.Value == 0;

        public static bool IsNotNullOrDef(this byte? value)
            => value.HasValue && value.Value != 0;

        // byte
        public static bool IsNullOrDef(this byte value)
            => value == 0;

        public static bool IsNotNullOrDef(this byte value)
            => value != 0;

        // DateTime?
        public static bool IsNullOrDef(this DateTime? value)
            => !value.HasValue || value.Value == DateTime.MinValue || value.Value == default;

        public static bool IsNotNullOrDef(this DateTime? value)
            => value.HasValue && value.Value != DateTime.MinValue && value.Value != default;

        // DateTime
        public static bool IsNullOrDef(this DateTime value)
            => value == DateTime.MinValue || value == default;

        public static bool IsNotNullOrDef(this DateTime value)
            => value != DateTime.MinValue && value != default;

        // Guid?
        public static bool IsNullOrDef(this Guid? value)
            => !value.HasValue || value.Value == Guid.Empty;

        public static bool IsNotNullOrDef(this Guid? value)
            => value.HasValue && value.Value != Guid.Empty;

        // Guid
        public static bool IsNullOrDef(this Guid value)
            => value == Guid.Empty;

        public static bool IsNotNullOrDef(this Guid value)
            => value != Guid.Empty;

        // Array
        public static bool IsNullOrDef<T>(this T[] value)
            => value == null || value.Length == 0;

        public static bool IsNotNullOrDef<T>(this T[] value)
            => value != null && value.Length > 0;

        // List<T>
        public static bool IsNullOrDef<T>(this List<T> value)
            => value == null || value.Count == 0;

        public static bool IsNotNullOrDef<T>(this List<T> value)
            => value != null && value.Count > 0;

        // ICollection<T>
        public static bool IsNullOrDef<T>(this ICollection<T> value)
            => value == null || value.Count == 0;

        public static bool IsNotNullOrDef<T>(this ICollection<T> value)
            => value != null && value.Count > 0;

        // IDictionary<K,V>
        public static bool IsNullOrDef<K, V>(this IDictionary<K, V> value)
            => value == null || value.Count == 0;

        public static bool IsNotNullOrDef<K, V>(this IDictionary<K, V> value)
            => value != null && value.Count > 0;

        // IEnumerable<T>
        public static bool IsNullOrDef<T>(this IEnumerable<T> value)
            => value == null || !value.Any();

        public static bool IsNotNullOrDef<T>(this IEnumerable<T> value)
            => value != null && value.Any();

        // ICollection (non-generic)
        public static bool IsNullOrDef(this ICollection value)
            => value == null || value.Count == 0;

        public static bool IsNotNullOrDef(this ICollection value)
            => value != null && value.Count > 0;

        // IDictionary (non-generic)
        public static bool IsNullOrDef(this IDictionary value)
            => value == null || value.Count == 0;

        public static bool IsNotNullOrDef(this IDictionary value)
            => value != null && value.Count > 0;

        // خاص: أقل من 1
        public static bool IsNullOrLessThanOne(this decimal? number)
            => !number.HasValue || number < 1m;

        public static bool IsNotNullOrGreaterOrEqualOne(this decimal? number)
            => number.HasValue && number >= 1m;

        public static bool IsNullOrLessThanOne(this decimal number)
            => number < 1m;

        public static bool IsNotNullOrGreaterOrEqualOne(this decimal number)
            => number >= 1m;

        public static bool IsNullOrLessThanOne(this int number)
            => number < 1;

        public static bool IsNotNullOrGreaterOrEqualOne(this int number)
            => number >= 1;

        public static bool IsNullOrLessThanOne(this int? number)
            => !number.HasValue || number < 1;

        public static bool IsNotNullOrGreaterOrEqualOne(this int? number)
            => number.HasValue && number >= 1;

        // تقريب أرقام عشرية
        public static decimal ToRound(this decimal value, int decimalPlaces)
            => Math.Round(value, decimalPlaces);
    }
}
