using System.Collections.Generic;
using System.Linq;
using CADLookup.Core.Contracts;
using CADLookup.Core.Domain;

namespace CADLookup.Core.Application
{
    public sealed class ObjectInspector : IObjectInspector
    {
        private readonly ICadHostAdapter _host;
        public ObjectInspector(ICadHostAdapter host) { _host = host; }

        public ObjectInspectionResult Inspect(CadObjectRef objectRef, InspectionOptions options)
        {
            var snapshot = _host.ReadObject(objectRef, new ReadOptions(
                options.IncludeExtendedData,
                maxProperties: options.MaxProperties,
                maxDepth: options.MaxDepth));
            var properties = snapshot.Properties.Take(options.MaxProperties).ToList();
            if (snapshot.Properties.Count > properties.Count)
                properties.Add(new CadProperty("…", "", "Additional properties omitted", "Inspector", PropertyStatus.Truncated));
            var boundedSnapshot = new CadObjectSnapshot(snapshot.Reference, snapshot.TypeName, snapshot.Layer,
                snapshot.State, properties, snapshot.Geometry, snapshot.Figures);
            return new ObjectInspectionResult(boundedSnapshot, properties);
        }
    }
}
