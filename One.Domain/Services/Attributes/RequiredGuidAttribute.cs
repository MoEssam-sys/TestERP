using System.ComponentModel.DataAnnotations;

namespace One.Domain.Services.Attributes
{
    /// <summary>
    /// RequiredGuid: لازم يكون GUID مش Empty
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class RequiredGuidAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value == null) return false;
            if (value is Guid g)
                return g != Guid.Empty;
            return false;
        }

        public override string FormatErrorMessage(string name)
            => $"{name} is required and cannot be empty GUID.";
    }
}