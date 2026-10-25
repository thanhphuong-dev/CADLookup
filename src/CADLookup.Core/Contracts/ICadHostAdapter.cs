using System.Collections.Generic;
using CADLookup.Core.Domain;

namespace CADLookup.Core.Contracts
{
    /// <summary>All methods are called synchronously on the CAD host's valid API context.</summary>
    public interface ICadHostAdapter
    {
        CadHostInfo Host { get; }
        CadDocumentInfo? GetActiveDocument();
        IReadOnlyList<CadObjectRef> SelectObjects(SelectionRequest request);
        CadObjectSnapshot ReadObject(CadObjectRef objectRef, ReadOptions options);
        CadObjectRef? FindObjectByHandle(string handle);
        IReadOnlyList<CadObjectRef> SnoopDatabase();
        IReadOnlyList<CadObjectRef> SearchObjects(SearchRequest request);
        /// <summary>Zooms the active drawing view to the object's extents. No-op for non-graphical objects.</summary>
        void ZoomToObject(CadObjectRef objectRef);
        /// <summary>Highlights the object in the drawing by making it the pickfirst selection.</summary>
        void SelectInDrawing(CadObjectRef objectRef);
        /// <summary>Draws a transient (non-database) highlight over the object; null clears any highlight.</summary>
        void SetTransientHighlight(CadObjectRef? objectRef);
    }
}
