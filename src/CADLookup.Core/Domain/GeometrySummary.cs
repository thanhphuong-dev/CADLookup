namespace CADLookup.Core.Domain
{
    public sealed class GeometrySummary
    {
        public GeometrySummary(string kind, string summary)
        {
            Kind = kind;
            Summary = summary;
        }
        public string Kind { get; }
        public string Summary { get; }
    }
}
