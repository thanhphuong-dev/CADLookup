namespace CADLookup.Core.Contracts
{
    public sealed class SearchRequest
    {
        public const int DefaultMaxResults = 200;

        public SearchRequest(string query, int maxResults = DefaultMaxResults)
        {
            Query = query ?? string.Empty;
            MaxResults = maxResults <= 0 ? DefaultMaxResults : maxResults;
        }

        /// <summary>Space-separated tokens; every token must match at least one searchable field.</summary>
        public string Query { get; }
        public int MaxResults { get; }
    }
}
