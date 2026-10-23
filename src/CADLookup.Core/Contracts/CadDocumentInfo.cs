namespace CADLookup.Core.Contracts
{
    public sealed class CadDocumentInfo
    {
        public CadDocumentInfo(string id, string name, string? path)
        {
            Id = id;
            Name = name;
            Path = path;
        }

        public string Id { get; }
        public string Name { get; }
        public string? Path { get; }
    }
}
