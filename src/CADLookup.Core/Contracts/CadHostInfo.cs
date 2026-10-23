namespace CADLookup.Core.Contracts
{
    public sealed class CadHostInfo
    {
        public CadHostInfo(string name, string version, string runtime, CadHostCapabilities capabilities = CadHostCapabilities.None)
        {
            Name = name;
            Version = version;
            Runtime = runtime;
            Capabilities = capabilities;
        }

        public string Name { get; }
        public string Version { get; }
        public string Runtime { get; }
        public CadHostCapabilities Capabilities { get; }
        public string DisplayName => string.IsNullOrWhiteSpace(Version) ? Name : Name + " " + Version;
    }
}
