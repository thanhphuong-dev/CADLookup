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
    }
}
