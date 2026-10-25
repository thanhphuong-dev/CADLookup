using CADLookup.Core.Application;

namespace CADLookup.UI.Search
{
    /// <summary>Matches every query token against any of the supplied fields, ignoring case and diacritics.</summary>
    public static class SearchMatcher
    {
        public static bool Matches(string? query, params string?[] fields) => TextMatcher.Matches(query, fields);

        public static string Normalize(string? value) => TextMatcher.Normalize(value);
    }
}
