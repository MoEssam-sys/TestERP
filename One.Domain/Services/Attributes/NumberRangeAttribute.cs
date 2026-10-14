using System.ComponentModel.DataAnnotations;

namespace One.Domain.Services.Attributes
{
    /// <summary>
    /// NumberRange: للأرقام (int, decimal, double..)
    /// min = أصغر قيمة مسموح بيها
    /// max = أكبر قيمة مسموح بيها
    /// لو min = 0 → مسموح يكون صفر
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class NumberRangeAttribute : ValidationAttribute
    {
        private readonly double _min;
        private readonly double _max;

        public NumberRangeAttribute(double min, double max)
        {
            _min = min;
            _max = max;
        }

        public override bool IsValid(object? value)
        {
            if (value == null) return _min == 0; // null is allowed only if min=0

            if (double.TryParse(value.ToString(), out var num))
            {
                if (_min == 0 && num == 0) return true;
                return num >= _min && num <= _max;
            }
            return false;
        }

        public override string FormatErrorMessage(string name)
            => $"{name} must be between {_min} and {_max}.";
    }
}