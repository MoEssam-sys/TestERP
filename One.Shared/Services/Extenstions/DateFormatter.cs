using System.Globalization;

namespace One.Shared.Services.Extenstions
{
    public static class DateFormatter
    {
        // 1. تحويل DateTime أو DateTime? لنص بالتنسيق المطلوب
        public static string ToDMYString(this DateTime date)
            => date.ToString("dd-MM-yyyy");

        public static string ToDMYString(this DateTime? date)
            => date?.ToString("dd-MM-yyyy") ?? "";

        // 2. تحويل نص إلى DateTime? (معالجة Null/Invalid)
        public static DateTime? ToDMYDate(this string? date)
        {
            if (string.IsNullOrWhiteSpace(date))
                return null;

            return DateTime.TryParseExact(date, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)
                ? result
                : (DateTime?)null;
        }
    }
}