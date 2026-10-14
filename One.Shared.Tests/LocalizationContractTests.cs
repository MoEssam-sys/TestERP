using System.Text.RegularExpressions;

namespace One.Shared.Tests;

public sealed partial class LocalizationContractTests
{
    [Fact]
    public void TranslationFiles_KeysAndPlaceholders_MatchExactly()
    {
        var resources = Path.Combine(AppContext.BaseDirectory, "Resources");
        var arabic = ReadTranslations(Path.Combine(resources, "ar.yml"));
        var english = ReadTranslations(Path.Combine(resources, "en-US.yml"));

        Assert.Equal(english.Keys.Order(), arabic.Keys.Order());
        foreach (var key in english.Keys)
        {
            Assert.Equal(
                Placeholders(english[key]),
                Placeholders(arabic[key]));
        }
    }

    private static Dictionary<string, string> ReadTranslations(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || char.IsWhiteSpace(line[0]) || line.TrimStart().StartsWith('#'))
                continue;

            var separator = line.IndexOf(':');
            if (separator <= 0)
                continue;

            var key = line[..separator].Trim();
            Assert.True(result.TryAdd(key, line[(separator + 1)..].Trim()), $"Duplicate translation key: {key}");
        }

        return result;
    }

    private static string[] Placeholders(string value) => PlaceholderExpression()
        .Matches(value)
        .Select(match => match.Value)
        .Order(StringComparer.Ordinal)
        .ToArray();

    [GeneratedRegex("\\{[^{}]+\\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderExpression();
}
