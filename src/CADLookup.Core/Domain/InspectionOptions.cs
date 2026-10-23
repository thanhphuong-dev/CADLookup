namespace CADLookup.Core.Domain
{
    public sealed class InspectionOptions
    {
        public InspectionOptions(int maxProperties = 300, int maxDepth = 6, bool includeExtendedData = true)
        {
            MaxProperties = maxProperties < 1 ? 1 : maxProperties;
            MaxDepth = maxDepth < 0 ? 0 : maxDepth;
            IncludeExtendedData = includeExtendedData;
        }
        public int MaxProperties { get; }
        public int MaxDepth { get; }
        public bool IncludeExtendedData { get; }
    }
}
