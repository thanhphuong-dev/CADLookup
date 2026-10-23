using CADLookup.Core.Domain;

namespace CADLookup.Core.Contracts
{
    public interface IObjectInspector
    {
        ObjectInspectionResult Inspect(CadObjectRef objectRef, InspectionOptions options);
    }
}
