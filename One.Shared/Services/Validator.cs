using System.Text.RegularExpressions;

namespace One.Shared.Services
{
    public static class Validator
    {
        public static IEnumerable<string> ValidString(string empty)
        {
            if (string.IsNullOrWhiteSpace(empty))
                yield return "Field is required";
        }

        public static IEnumerable<string> ValidStringNumber(string empty)
        {
            if (string.IsNullOrWhiteSpace(empty))
                yield return "Field is required";
            else
            {
                var num = double.Parse(empty);
                if (num <= 0)
                {
                    yield return "Cannot be less than 0";
                }
            }
        }

        public static IEnumerable<string> ValidInt(int empty)
        {
            if (empty <= 0)
                yield return "Field is required";
        }

        public static IEnumerable<string> ValidInt(int? empty)
        {
            if (empty is null || empty <= 0)
                yield return "Field is required";
        }

        public static IEnumerable<string> ValidDecimal(decimal empty)
        {
            if (empty <= 0)
                yield return "Field is required";
        }

        public static IEnumerable<string> ValidDecimal(decimal? empty)
        {
            if (empty is null || empty <= 0)
                yield return "Field is required";
        }

        public static IEnumerable<string> ValidDecimal(float empty)
        {
            if (empty <= 0)
                yield return "Field is required";
        }

        public static IEnumerable<string> ValidDecimal(float? empty)
        {
            if (empty is null || empty <= 0)
                yield return "Field is required";
        }

        // Regex Patterns
        public static async Task<bool> IsValidRgx(this string pattern, Regex rgx)
        {
            return await Task.FromResult(rgx.IsMatch(pattern));
        }

        public static async Task<bool> IsValidEmail(this string email)
        {
            var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
            return await Task.FromResult(emailRegex.IsMatch(email));
        }

        public static IEnumerable<string> ValidMobile(string input)
        {
            var errors = new List<string>();

            // Define the phone number regex
            var phoneRegex = @"^\+?(\d{1,4})?[-.\s]?((\d{1,3})[-.\s]?){1,4}(\d+)$";

            if (string.IsNullOrEmpty(input) || !Regex.IsMatch(input, phoneRegex))
            {
                errors.Add("Please enter a valid mobile or phone number.");
            }

            return errors;
        }
    }
}