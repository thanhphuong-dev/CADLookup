namespace CADLookup.Core.Contracts
{
    public sealed class ReadOptions
    {
        public ReadOptions(bool includeExtendedData = true, int maxExtendedDataItems = 200, int maxProperties = 300, int maxDepth = 6, int maxCollectionItems = 100)
        {
            IncludeExtendedData = includeExtendedData;
            MaxExtendedDataItems = maxExtendedDataItems < 0 ? 0 : maxExtendedDataItems;
            MaxProperties = maxProperties < 1 ? 1 : maxProperties;
            MaxDepth = maxDepth < 0 ? 0 : maxDepth;
            MaxCollectionItems = maxCollectionItems < 0 ? 0 : maxCollectionItems;
        }

        public bool IncludeExtendedData { get; }
        public int MaxExtendedDataItems { get; }
        public int MaxProperties { get; }
        public int MaxDepth { get; }
        public int MaxCollectionItems { get; }
    }
}
