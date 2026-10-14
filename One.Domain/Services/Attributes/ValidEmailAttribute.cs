using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace One.Domain.Services.Attributes
{
    /// <summary>
    /// ValidEmail: Email address بسيط
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ValidEmailAttribute : ValidationAttribute
    {
        private static readonly Regex _regex =
            new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public override bool IsValid(object? value)
        {
            if (value == null) return true; // ممكن يبقى null
            var str = value.ToString()?.Trim();
            if (string.IsNullOrEmpty(str)) return true; // null/empty is allowed

            return _regex.IsMatch(str);
        }

        public override string FormatErrorMessage(string name)
            => $"{name} is not a valid email address.";
    }
}