namespace CADLookup.Core.Contracts
{
    public sealed class CadObjectRef
    {
        public CadObjectRef(string hostName, string documentId, string handle, string nativeId, string typeName, string memberPath = "", string layer = "")
        {
            HostName = hostName;
            DocumentId = documentId;
            Handle = handle;
            NativeId = nativeId;
            TypeName = typeName;
            MemberPath = memberPath ?? string.Empty;
            Layer = layer ?? string.Empty;
        }

        public string HostName { get; }
        public string DocumentId { get; }
        public string Handle { get; }
        public string NativeId { get; }
        public string TypeName { get; }
        /// <summary>Host-neutral, serializable property/index route from the native database object.</summary>
        public string MemberPath { get; }
        public string Layer { get; }
        public int NavigationDepth => string.IsNullOrEmpty(MemberPath) ? 0 : MemberPath.Split('/').Length;
        public string DisplayName => TypeName + "  [" + Handle + "]" + (string.IsNullOrEmpty(MemberPath) ? string.Empty : " · " + MemberPath);
    }
}
