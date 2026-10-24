using System;

namespace CADLookup.Core.Contracts
{
    [Flags]
    public enum CadHostCapabilities
    {
        None = 0,
        ObjectSelection = 1 << 0,
        HandleLookup = 1 << 1,
        ObjectRead = 1 << 2,
        GeometrySummary = 1 << 3,
        ExtendedData = 1 << 4,
        ReflectedProperties = 1 << 5,
        MethodCatalog = 1 << 6,
        EventCatalog = 1 << 7,
        RelationshipNavigation = 1 << 8,
        ModelessInspector = 1 << 9,
        JsonExport = 1 << 10,
        TextInspection = 1 << 11,
        BlockAttributeInspection = 1 << 12,
        DatabaseSnoop = 1 << 13
    }
}
