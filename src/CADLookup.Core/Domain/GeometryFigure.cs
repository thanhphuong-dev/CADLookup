using System.Collections.Generic;

namespace CADLookup.Core.Domain
{
    /// <summary>A host-neutral drawable figure: a polyline or a point set in drawing coordinates (Y up).</summary>
    public sealed class GeometryFigure
    {
        public const string PolylineKind = "Polyline";
        public const string PointsKind = "Points";

        public GeometryFigure(string kind, IEnumerable<double> coordinates, bool closed = false)
        {
            Kind = kind == PointsKind ? PointsKind : PolylineKind;
            Coordinates = coordinates as IReadOnlyList<double> ?? new List<double>(coordinates);
            Closed = closed;
        }

        public string Kind { get; }
        /// <summary>Flat X,Y pairs: x0, y0, x1, y1, …</summary>
        public IReadOnlyList<double> Coordinates { get; }
        public bool Closed { get; }
        public int PointCount => Coordinates.Count / 2;
    }
}
