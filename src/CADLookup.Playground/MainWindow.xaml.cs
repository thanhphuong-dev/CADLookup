using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CADLookup.Core.Application;
using CADLookup.Core.Contracts;
using CADLookup.Core.Domain;
using CADLookup.UI.Localization;
using CADLookup.UI.ViewModels;
using CADLookup.UI.Views;

namespace CADLookup.Playground
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var adapter = new FakeCadHostAdapter();
            var viewModel = new MainViewModel(adapter, new ObjectInspector(adapter), new LocalizationService());
            Content = new CADLookup.UI.Views.MainView(viewModel);
            Title = "CADLookup Playground ??fake CAD host";
        }

    }

    internal sealed class FakeCadHostAdapter : ICadHostAdapter
    {
        private readonly List<CadObjectRef> _objects = new List<CadObjectRef>
        {
            new CadObjectRef("Playground", "sample.dwg", "1A2", "mock-1", "Line"),
            new CadObjectRef("Playground", "sample.dwg", "1A3", "mock-2", "Circle"),
            new CadObjectRef("Playground", "sample.dwg", "1A4", "mock-3", "Polyline")
        };
        private readonly Dictionary<string, FakeEntityModel> _models = new Dictionary<string, FakeEntityModel>
        {
            ["1A2"] = new FakeEntityModel("Line", "A-WALL", new FakeGeometry("Line", 1250, 720, 1442.22)),
            ["1A3"] = new FakeEntityModel("Circle", "EQUIPMENT", new FakeGeometry("Circle", 500, 350, 125)),
            ["1A4"] = new FakeEntityModel("Polyline", "A-WALL", new FakeGeometry("Polyline", 0, 0, 3875))
        };
        public CadHostInfo Host => new CadHostInfo("CADLookup Playground", "1.0", ".NET Framework 4.8",
            CadHostCapabilities.ObjectSelection | CadHostCapabilities.HandleLookup | CadHostCapabilities.ObjectRead |
            CadHostCapabilities.GeometrySummary | CadHostCapabilities.ExtendedData | CadHostCapabilities.ReflectedProperties |
            CadHostCapabilities.MethodCatalog | CadHostCapabilities.EventCatalog | CadHostCapabilities.JsonExport |
            CadHostCapabilities.TextInspection | CadHostCapabilities.BlockAttributeInspection | CadHostCapabilities.DatabaseSnoop |
            CadHostCapabilities.ZoomToObject | CadHostCapabilities.SelectInDrawing | CadHostCapabilities.DatabaseSearch);
        public CadDocumentInfo GetActiveDocument() => new CadDocumentInfo("sample.dwg", "sample.dwg", "C:\\Samples\\sample.dwg");
        public IReadOnlyList<CadObjectRef> SelectObjects(SelectionRequest request) => _objects;
        public CadObjectRef? FindObjectByHandle(string handle) => _objects.FirstOrDefault(x => x.Handle.Equals(handle, StringComparison.OrdinalIgnoreCase));
        public IReadOnlyList<CadObjectRef> SnoopDatabase() => new List<CadObjectRef>
        {
            new CadObjectRef("Playground", "sample.dwg", "1A2", "mock-1", "BlockTableRecord", layer: "*Model_Space"),
            new CadObjectRef("Playground", "sample.dwg", "1A3", "mock-2", "LayerTableRecord", layer: "A-WALL"),
            new CadObjectRef("Playground", "sample.dwg", "1A4", "mock-3", "TextStyleTableRecord", layer: "Standard"),
            new CadObjectRef("Playground", "sample.dwg", "1A2", "mock-1", "LinetypeTableRecord", layer: "Continuous"),
            new CadObjectRef("Playground", "sample.dwg", "1A3", "mock-2", "DBDictionary", layer: "NamedObjectsDictionary")
        };
        public IReadOnlyList<CadObjectRef> SearchObjects(SearchRequest request) =>
            string.IsNullOrWhiteSpace(request.Query) ? Array.Empty<CadObjectRef>() : _objects;
        public void ZoomToObject(CadObjectRef objectRef) { }
        public void SelectInDrawing(CadObjectRef objectRef) { }
        public void SetTransientHighlight(CadObjectRef? objectRef) { }
        public CadObjectSnapshot ReadObject(CadObjectRef reference, ReadOptions options)
        {
            var root = _models[reference.Handle];
            var ancestors = new List<object> { root };
            object target;
            try { target = ReflectionMemberReader.ResolvePath(root, reference.MemberPath, ancestors); }
            catch (Exception ex)
            {
                var status = ex is ReflectionMemberReader.CyclicMemberPathException ? PropertyStatus.CyclicReference : PropertyStatus.EvaluationError;
                var failure = new CadProperty("Member evaluation", reference.TypeName, ex.Message, "Reflection", status);
                return new CadObjectSnapshot(reference, reference.TypeName, root.Layer, "Available", new[] { failure });
            }
            var props = new List<CadProperty>();
            if (string.IsNullOrEmpty(reference.MemberPath))
            {
                props.AddRange(new[]
                {
                    new CadProperty("Handle", "Handle", reference.Handle, "Identity"),
                    new CadProperty("Object type", "String", reference.TypeName, "Runtime"),
                    new CadProperty("Layer", "String", root.Layer, "Entity"),
                    new CadProperty("Visible", "Boolean", "True", "Entity"),
                    new CadProperty("Color", "Color", "ByLayer", "Entity"),
                    new CadProperty("Extended data", "String", "APP_DATA: sample value", "Extended data")
                });
            }
            var geometry = string.IsNullOrEmpty(reference.MemberPath)
                ? new List<GeometrySummary> { new GeometrySummary("Bounds", "Min (0, 0, 0) 쨌 Max (1250, 720, 0)") }
                : new List<GeometrySummary>();
            if (reference.MemberPath.Length == 0 && reference.TypeName == "Line")
            {
                props.Add(new CadProperty("Start point", "Point3d", "X=0, Y=0, Z=0", "Geometry"));
                props.Add(new CadProperty("End point", "Point3d", "X=1250, Y=720, Z=0", "Geometry"));
                props.Add(new CadProperty("Length", "Double", "1442.2205101856", "Geometry"));
            }
            else if (reference.MemberPath.Length == 0 && reference.TypeName == "Circle")
            {
                props.Add(new CadProperty("Center", "Point3d", "X=500, Y=350, Z=0", "Geometry"));
                props.Add(new CadProperty("Radius", "Double", "125", "Geometry"));
                props.Add(new CadProperty("Area", "Double", "49087.3852123405", "Geometry"));
            }
            else if (reference.MemberPath.Length == 0)
            {
                props.Add(new CadProperty("Vertex count", "Int32", "5", "Geometry"));
                props.Add(new CadProperty("Closed", "Boolean", "True", "Geometry"));
                props.Add(new CadProperty("Length", "Double", "3875.0", "Geometry"));
            }
            var room = Math.Max(1, options.MaxProperties - props.Count);
            props.AddRange(ReflectionMemberReader.Read(target, reference,
                new ReadOptions(options.IncludeExtendedData, options.MaxExtendedDataItems, room,
                    options.MaxDepth, options.MaxCollectionItems), ancestors));
            return new CadObjectSnapshot(reference, target.GetType().Name, root.Layer, "Available", props, geometry);
        }
    }

    internal sealed class FakeEntityModel
    {
        public FakeEntityModel(string entityName, string layer, FakeGeometry geometry)
        {
            EntityName = entityName;
            Layer = layer;
            Geometry = geometry;
        }
        public string EntityName { get; }
        public string Layer { get; }
        public bool Visible => true;
        public FakeGeometry Geometry { get; }
        public FakePoint Origin => Geometry.Origin;
        public IReadOnlyList<string> Tags => new[] { "sample", "playground", EntityName.ToLowerInvariant() };
        public FakeEntityModel Self => this;
        public event EventHandler? Changed { add { } remove { } }
        public string Describe(string format) => EntityName + ":" + format;
    }

    internal sealed class FakeGeometry
    {
        public FakeGeometry(string kind, double x, double y, double length)
        {
            Kind = kind;
            Origin = new FakePoint(x, y, 0);
            Length = length;
        }
        public string Kind { get; }
        public FakePoint Origin { get; }
        public double Length { get; }
    }

    internal sealed class FakePoint
    {
        public FakePoint(double x, double y, double z) { X = x; Y = y; Z = z; }
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
    }
}

