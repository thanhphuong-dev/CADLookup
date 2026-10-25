using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CADLookup.Core.Application
{
    /// <summary>Matches every query token against any of the supplied fields, ignoring case and diacritics.</summary>
    public static class TextMatcher
    {
        public static bool Matches(string? query, params string?[] fields)
        {
            var normalizedQuery = Normalize(query);
            if (normalizedQuery.Length == 0) return true;

            var tokens = normalizedQuery.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var normalizedFields = (fields ?? Array.Empty<string?>())
                .Where(field => !string.IsNullOrWhiteSpace(field))
                .Select(Normalize)
                .ToArray();

            return tokens.All(token => normalizedFields.Any(field => field.IndexOf(token, StringComparison.Ordinal) >= 0));
        }

        public static string Normalize(string? value)
        {
            var input = value ?? string.Empty;
            if (input.Length == 0) return string.Empty;

            var decomposed = input.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            var previousWasWhitespace = true;

            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                    continue;

                if (char.IsWhiteSpace(character))
                {
                    if (!previousWasWhitespace) builder.Append(' ');
                    previousWasWhitespace = true;
                    continue;
                }

                builder.Append(char.ToLowerInvariant(character));
                previousWasWhitespace = false;
            }

            if (builder.Length > 0 && builder[builder.Length - 1] == ' ')
                builder.Length--;

            return builder.ToString();
        }
    }
}
