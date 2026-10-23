using System.Collections.Generic;
using CADLookup.Core.Contracts;

namespace CADLookup.Core.Domain
{
    public sealed class ObjectInspectionResult
    {
        public ObjectInspectionResult(CadObjectSnapshot snapshot, IEnumerable<CadProperty> properties)
        {
            Snapshot = snapshot;
            Properties = new List<CadProperty>(properties);
        }
        public CadObjectSnapshot Snapshot { get; }
        public IReadOnlyList<CadProperty> Properties { get; }
    }
}
