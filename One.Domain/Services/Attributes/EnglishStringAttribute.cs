using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace One.Domain.Services.Attributes
{
    /// <summary>
    /// Validates that:
    /// - Code starts with an English letter (not a digit)
    /// - Only English letters, digits, and the configured symbols are allowed
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class EnglishStringAttribute : ValidationAttribute
    {
        /// <summary>
        /// Symbols allowed in addition to letters and digits. Default: -_.
        /// </summary>
        public string AllowedSymbols { get; set; } = "-_.";

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is null) return ValidationResult.Success; // use [Required] separately if needed
            if (value is not string s)
                return new ValidationResult($"{validationContext.DisplayName} must be a string.");

            s = s.Trim();
            if (s.Length == 0) return ValidationResult.Success;

            var escaped = Regex.Escape(AllowedSymbols ?? string.Empty);
            var pattern = $"^(?!\\d)[A-Za-z][A-Za-z0-9{escaped}]*$";

            if (Regex.IsMatch(s, pattern))
                return ValidationResult.Success;

            var msg = ErrorMessage ??
                      $"{validationContext.DisplayName} must start with a letter and contain only English letters, digits, or these symbols: {AllowedSymbols}";
            return new ValidationResult(msg);
        }
    }
}