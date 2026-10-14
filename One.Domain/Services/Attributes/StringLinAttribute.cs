using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace One.Domain.Services.Attributes
{
    /// <summary>
    /// StringLin: Trim + Min/Max length
    /// لو min = 0 → ممكن يبقى null أو فاضي
    /// لو MemberName == "Code" → يتحقق انه يبدأ بحرف انجليزي Capital
    ///     والباقي حروف انجليزية/أرقام/رموز مسموح بها
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class StringLinAttribute : ValidationAttribute
    {
        private readonly int _min;
        private readonly int _max;

        public StringLinAttribute(int min, int max)
        {
            _min = min;
            _max = max;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var str = value as string;
            if (str != null)
            {
                str = str.Trim();

                // Required check
                if (_min > 0 && string.IsNullOrEmpty(str))
                    return new ValidationResult($"{validationContext.MemberName} is required (min length {_min}).");

                if (!string.IsNullOrEmpty(str))
                {
                    if (str.Length < _min)
                        return new ValidationResult($"{validationContext.MemberName} must be at least {_min} characters.");

                    if (str.Length > _max)
                        return new ValidationResult($"{validationContext.MemberName} must not exceed {_max} characters.");

                    // --- Special rule for Code ---
                    if (string.Equals(validationContext.MemberName, "Code", StringComparison.OrdinalIgnoreCase))
                    {
                        // Pattern: start with uppercase English letter, followed by letters, digits or -_.
                        var pattern = @"^[A-Z][A-Za-z0-9._-]*$";
                        if (!Regex.IsMatch(str, pattern))
                        {
                            return new ValidationResult(
                                $"{validationContext.MemberName} must start with an uppercase English letter and contain only English letters, digits, or -_."
                            );
                        }
                    }
                }
            }
            else if (_min > 0)
            {
                return new ValidationResult($"{validationContext.MemberName} is required.");
            }

            return ValidationResult.Success;
        }
    }
}