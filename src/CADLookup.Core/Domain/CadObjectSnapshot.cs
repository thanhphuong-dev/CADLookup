using System.Collections.Generic;
using CADLookup.Core.Contracts;

namespace CADLookup.Core.Domain
{
    public sealed class CadObjectSnapshot
    {
        public CadObjectSnapshot(CadObjectRef reference, string typeName, string layer, string state, IEnumerable<CadProperty> properties,
            IEnumerable<GeometrySummary>? geometry = null, IEnumerable<GeometryFigure>? figures = null)
        {
            Reference = reference;
            TypeName = typeName;
            Layer = layer;
            State = state;
            Properties = new List<CadProperty>(properties);
            Geometry = geometry == null ? new List<GeometrySummary>() : new List<GeometrySummary>(geometry);
            Figures = figures == null ? new List<GeometryFigure>() : new List<GeometryFigure>(figures);
        }

        public CadObjectRef Reference { get; }
        public string TypeName { get; }
        public string Layer { get; }
        public string State { get; }
        public IReadOnlyList<CadProperty> Properties { get; }
        public IReadOnlyList<GeometrySummary> Geometry { get; }
        public IReadOnlyList<GeometryFigure> Figures { get; }
    }
}
