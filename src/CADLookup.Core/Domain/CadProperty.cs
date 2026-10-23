using CADLookup.Core.Contracts;

namespace CADLookup.Core.Domain
{
    public enum PropertyStatus { Available, NotEvaluated, Unsupported, EvaluationError, Truncated, CyclicReference }

    public sealed class CadProperty
    {
        public CadProperty(string name, string typeName, string value, string source, PropertyStatus status = PropertyStatus.Available, CadObjectRef? reference = null)
        {
            Name = name;
            TypeName = typeName;
            Value = value;
            Source = source;
            Status = status;
            Reference = reference;
        }

        public string Name { get; }
        public string TypeName { get; }
        public string Value { get; }
        public string Source { get; }
        public PropertyStatus Status { get; }
        public CadObjectRef? Reference { get; }
    }
}
