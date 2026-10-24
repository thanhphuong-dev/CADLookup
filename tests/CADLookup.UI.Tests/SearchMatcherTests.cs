using CADLookup.UI.Search;
using Xunit;

namespace CADLookup.UI.Tests
{
    public sealed class SearchMatcherTests
    {
        [Theory]
        [InlineData(null, "Anything")]
        [InlineData("", "Anything")]
        [InlineData("   ", "Anything")]
        public void Empty_query_matches_everything(string? query, string field)
        {
            Assert.True(SearchMatcher.Matches(query, field));
        }

        [Theory]
        [InlineData("BẢN VẼ", "Bản vẽ kỹ thuật")]
        [InlineData("ban ve", "Bản vẽ kỹ thuật")]
        [InlineData("DUONG", "Đường tròn")]
        [InlineData("duong tron", "Đường tròn")]
        [InlineData("đường", "Đường tròn")]
        public void Matching_ignores_case_and_vietnamese_diacritics(string query, string field)
        {
            Assert.True(SearchMatcher.Matches(query, field));
        }

        [Theory]
        [InlineData("ban ve kho", "Bản vẽ kỹ thuật")] // "kho" not present
        [InlineData("xyz", "Bản vẽ kỹ thuật")]
        public void Non_matching_queries_are_rejected(string query, string field)
        {
            Assert.False(SearchMatcher.Matches(query, field));
        }

        [Fact]
        public void All_tokens_must_match_across_any_field()
        {
            Assert.True(SearchMatcher.Matches("circle 12.5", "Name", "Circle", "Radius 12.5"));
            Assert.False(SearchMatcher.Matches("circle 99", "Name", "Circle", "Radius 12.5"));
        }

        [Fact]
        public void Normalize_strips_diacritics_and_lowercases()
        {
            Assert.Equal("ban ve", SearchMatcher.Normalize("BẢN VẼ"));
            Assert.Equal("duong", SearchMatcher.Normalize("Đường"));
            Assert.Equal("abc", SearchMatcher.Normalize("ÁBÇ"));
        }
    }
}
