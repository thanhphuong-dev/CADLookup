using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Windows;
using CADLookup.Core.Contracts;
using CADLookup.Core.Domain;
using CADLookup.Host.AutoCAD;
using System.Windows;
using System.Windows.Interop;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace CADLookup.Host.AutoCAD.Smoke
{
    /// <summary>Run inside AutoCAD to verify Ribbon registration and the host selection-to-inspector path.</summary>
    public sealed class SmokeCommands
    {
        [CommandMethod("CADLOOKUPSMOKE")]
        public void VerifyLoadedInspector()
        {
            var reportPath = Path.Combine(Path.GetTempPath(), "CADLookup-AutoCAD-smoke.txt");
            try
            {
                var document = AcadApp.DocumentManager.MdiActiveDocument
                    ?? throw new InvalidOperationException("No active drawing.");
                SampleSet samples = CreateSamples(document);
                string expectedHandle = samples.LineHandle;
                const string selectedType = "Line";
                document.Editor.SetImpliedSelection(new[] { samples.LineId });

                var extensionType = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => assembly.GetName().Name == "CADLookup.Host.AutoCAD")
                    .Select(assembly => assembly.GetType("CADLookup.Host.AutoCAD.CadLookupCommands", false))
                    .FirstOrDefault(type => type != null)
                    ?? throw new InvalidOperationException("CADLookup.Host.AutoCAD is not loaded.");
                var command = Activator.CreateInstance(extensionType)
                    ?? throw new InvalidOperationException("Could not create the CADLookup command entry point.");
                extensionType.GetMethod("ShowCadLookup")?.Invoke(command, null);
                const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
                var viewModel = extensionType.GetField("_viewModel", flags)?.GetValue(null)
                    ?? throw new InvalidOperationException("CADLOOKUP did not create its view model.");
                var window = extensionType.GetField("_window", flags)?.GetValue(null) as Window
                    ?? throw new InvalidOperationException("CADLOOKUP did not create its WPF window.");

                var vmType = viewModel.GetType();
                var selectedHandle = vmType.GetProperty("SelectedHandle")?.GetValue(viewModel)?.ToString();
                var visibleObjects = vmType.GetProperty("Objects")?.GetValue(viewModel) as ICollection;
                var properties = vmType.GetProperty("Properties")?.GetValue(viewModel) as IEnumerable;
                var propertyNames = properties?.Cast<object>()
                    .Select(item => item.GetType().GetProperty("Name")?.GetValue(item)?.ToString() ?? string.Empty)
                    .ToArray() ?? Array.Empty<string>();
                var geometry = vmType.GetProperty("Geometry")?.GetValue(viewModel) as ICollection;
                var ribbonTab = ComponentManager.Ribbon?.Tabs.FirstOrDefault(item => item.Id == "CADLOOKUP_TAB");
                var ribbonButtons = ribbonTab?.Panels.SelectMany(item => item.Source.Items)
                    .OfType<RibbonButton>().ToArray() ?? Array.Empty<RibbonButton>();

                Require(window.IsVisible, "The inspector window is not visible.");
                Require(new WindowInteropHelper(window).Owner == AcadApp.MainWindow.Handle,
                    "The inspector window is not owned by the AutoCAD main window.");
                Require(ribbonTab != null && ribbonButtons.Any(item => item.Id == "CADLOOKUP_OPEN_BUTTON") &&
                    ribbonButtons.Any(item => item.Id == "CADLOOKUP_SELECT_BUTTON") &&
                    ribbonButtons.Any(item => item.Id == "CADLOOKUP_REFRESH_BUTTON") &&
                    ribbonButtons.Any(item => item.Id == "CADLOOKUP_LANGUAGE_BUTTON"),
                    "The CADLookup Ribbon controls were not registered.");
                Require(string.Equals(expectedHandle, selectedHandle, StringComparison.OrdinalIgnoreCase),
                    "Selected handle mismatch. Expected " + expectedHandle + ", got " + selectedHandle + ".");
                Require(visibleObjects != null && visibleObjects.Count > 0, "The object list is empty.");
                Require(propertyNames.Contains("Handle"), "The Handle property is missing.");
                if (selectedType == "Line")
                    Require(propertyNames.Contains("Length") && propertyNames.Contains("Start point") &&
                        geometry != null && geometry.Count > 0, "Line geometry properties were not loaded.");
                VerifyAdapterCoverage(new AutoCadHostAdapter(), samples);
                var hostActions = VerifyHostActions(new AutoCadHostAdapter(), samples, document);

                window.Close();
                document.Editor.SetImpliedSelection(new[] { samples.LineId });
                extensionType.GetMethod("SelectForCadLookup")?.Invoke(command, null);
                var reopenedWindow = extensionType.GetField("_window", flags)?.GetValue(null) as Window;
                Require(reopenedWindow != null && reopenedWindow.IsVisible && !ReferenceEquals(window, reopenedWindow),
                    "The Ribbon selection command did not reopen the inspector window after selection.");

                File.WriteAllText(reportPath,
                    "PASS\r\nDocument=" + document.Name + "\r\nHandle=" + expectedHandle +
                    "\r\nType=" + selectedType + "\r\nWindowVisible=" + window.IsVisible +
                    "\r\nWindowOwner=" + new WindowInteropHelper(window).Owner +
                    "\r\nRibbonTab=" + ribbonTab!.Title + "\r\nRibbonButtons=" + string.Join(",", ribbonButtons.Select(item => item.Text)) +
                    "\r\nObjectCount=" + visibleObjects!.Count + "\r\nPropertyCount=" + propertyNames.Length +
                    "\r\nGeometryCount=" + (geometry?.Count ?? 0) + "\r\nAdapterCoverage=Line,Circle,Polyline,Text,Block,Layer,Erased" +
                    "\r\n" + hostActions +
                    "\r\nProperties=" + string.Join(",", propertyNames));
                document.Editor.WriteMessage("\nCADLookup AutoCAD smoke test: PASS. Report: {0}", reportPath);
            }
            catch (System.Exception exception)
            {
                File.WriteAllText(reportPath, "FAIL\r\n" + exception);
                AcadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                    "\nCADLookup AutoCAD smoke test: FAIL. Report: {0}\n{1}", reportPath, exception.Message);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void VerifyAdapterCoverage(AutoCadHostAdapter adapter, SampleSet samples)
        {
            VerifyProperties(adapter, samples.LineHandle, "Line", "Length", "Start point", "End point");
            VerifyProperties(adapter, samples.CircleHandle, "Circle", "Radius", "Area", "Center");
            VerifyProperties(adapter, samples.PolylineHandle, "Polyline", "Vertex count", "Closed", "Length");
            VerifyProperties(adapter, samples.TextHandle, "DBText", "Text", "Position", "Height");
            VerifyProperties(adapter, samples.BlockHandle, "BlockReference", "Block name", "Attribute SMOKE_TAG");
            VerifyProperties(adapter, samples.LayerHandle, "LayerTableRecord", "Name", "Frozen", "Locked");

            var lineFigures = Read(adapter, samples.LineHandle).Figures;
            Require(lineFigures.Count == 1 && lineFigures[0].Kind == GeometryFigure.PolylineKind && lineFigures[0].PointCount == 2,
                "The smoke line did not produce exactly one two-point polyline figure.");
            var circleFigures = Read(adapter, samples.CircleHandle).Figures;
            Require(circleFigures.Count > 0 && circleFigures[0].Closed && circleFigures[0].PointCount > 8,
                "The smoke circle was not approximated as a closed multi-point figure.");
            var blockFigures = Read(adapter, samples.BlockHandle).Figures;
            Require(blockFigures.Count == 1 && blockFigures[0].Closed,
                "The smoke block reference did not produce a closed extents-box figure.");

            var erased = Read(adapter, samples.ErasedHandle);
            Require(string.Equals(erased.State, "Erased", StringComparison.OrdinalIgnoreCase),
                "An erased object was not reported as erased.");
        }

        /// <summary>Exercises whole-drawing search, zoom-to-object, select-in-drawing and transient highlight.</summary>
        private static string VerifyHostActions(AutoCadHostAdapter adapter, SampleSet samples, Document document)
        {
            var textRef = adapter.FindObjectByHandle(samples.TextHandle)
                ?? throw new InvalidOperationException("Smoke text was not found for search coverage.");
            var circleRef = adapter.FindObjectByHandle(samples.CircleHandle)
                ?? throw new InvalidOperationException("Smoke circle was not found for zoom coverage.");
            var blockRef = adapter.FindObjectByHandle(samples.BlockHandle)
                ?? throw new InvalidOperationException("Smoke block was not found for search coverage.");

            var textHits = adapter.SearchObjects(new SearchRequest("text cadlookup"));
            Require(textHits.Any(item => string.Equals(item.Handle, samples.TextHandle, StringComparison.OrdinalIgnoreCase)),
                "Whole-drawing search did not find the smoke text via query 'text cadlookup'.");
            var blockHits = adapter.SearchObjects(new SearchRequest("smoke_tag smoke value"));
            Require(blockHits.Any(item => string.Equals(item.Handle, samples.BlockHandle, StringComparison.OrdinalIgnoreCase)),
                "Whole-drawing search did not find the smoke block via its attribute values.");
            var vietnameseHits = adapter.SearchObjects(new SearchRequest("cadlookup smoke"));
            Require(vietnameseHits.Count > 0, "Whole-drawing search returned no results for a valid query.");
            var emptyHits = adapter.SearchObjects(new SearchRequest("zzz-not-present"));
            Require(emptyHits.Count == 0, "Whole-drawing search returned false positives for a non-matching query.");

            var viewBefore = document.Editor.GetCurrentView();
            adapter.ZoomToObject(circleRef);
            var viewAfter = document.Editor.GetCurrentView();
            var centerOffset = Math.Abs(viewAfter.CenterPoint.X - 150);
            Require(centerOffset < Math.Max(viewAfter.Width / 2.0, 1e-6),
                "Zoom-to-object did not center the view on the smoke circle (offset " + centerOffset.ToString("G", System.Globalization.CultureInfo.InvariantCulture) + ").");

            adapter.SelectInDrawing(circleRef);
            var implied = document.Editor.SelectImplied();
            Require(implied != null && implied.Status == PromptStatus.OK && implied.Value != null && implied.Value.Count == 1 &&
                string.Equals(implied.Value.GetObjectIds()[0].Handle.ToString(), circleRef.Handle, StringComparison.OrdinalIgnoreCase),
                "Select-in-drawing did not make the smoke circle the pickfirst selection.");

            adapter.SetTransientHighlight(textRef);
            adapter.SetTransientHighlight(null);

            return "Search=text:" + textHits.Count + ",block:" + blockHits.Count +
                ",negative:" + emptyHits.Count +
                "\r\nZoomCenterOffset=" + centerOffset.ToString("G", System.Globalization.CultureInfo.InvariantCulture) +
                "\r\nSelectInDrawing=pickfirst-ok" +
                "\r\nTransientHighlight=set,cleared";
        }

        private static void VerifyProperties(AutoCadHostAdapter adapter, string handle, string description, params string[] names)
        {
            var snapshot = Read(adapter, handle);
            var propertyNames = snapshot.Properties.Select(item => item.Name).ToArray();
            foreach (var name in names)
                Require(propertyNames.Contains(name), description + " is missing the '" + name + "' property.");
        }

        private static CadObjectSnapshot Read(AutoCadHostAdapter adapter, string handle)
        {
            var reference = adapter.FindObjectByHandle(handle) ?? throw new InvalidOperationException("Handle " + handle + " was not found.");
            return adapter.ReadObject(reference, new ReadOptions(maxProperties: 300));
        }

        private static SampleSet CreateSamples(Document document)
        {
            var samples = new SampleSet();
            using (var transaction = document.Database.TransactionManager.StartOpenCloseTransaction())
            {
                var layerTable = (LayerTable)transaction.GetObject(document.Database.LayerTableId, OpenMode.ForRead);
                layerTable.UpgradeOpen();
                var layer = new LayerTableRecord { Name = "CADLOOKUP_SMOKE_LAYER", Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 3) };
                layerTable.Add(layer);
                transaction.AddNewlyCreatedDBObject(layer, true);
                samples.LayerHandle = layer.Handle.ToString();

                var blockTable = (BlockTable)transaction.GetObject(document.Database.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                var line = new Line(new Point3d(0, 0, 0), new Point3d(100, 0, 0));
                samples.LineId = modelSpace.AppendEntity(line);
                transaction.AddNewlyCreatedDBObject(line, true);
                samples.LineHandle = line.Handle.ToString();

                var circle = new Circle(new Point3d(150, 0, 0), Vector3d.ZAxis, 25);
                modelSpace.AppendEntity(circle);
                transaction.AddNewlyCreatedDBObject(circle, true);
                samples.CircleHandle = circle.Handle.ToString();

                var polyline = new Polyline();
                polyline.AddVertexAt(0, new Point2d(0, 40), 0, 0, 0);
                polyline.AddVertexAt(1, new Point2d(40, 40), 0, 0, 0);
                polyline.AddVertexAt(2, new Point2d(40, 80), 0, 0, 0);
                polyline.Closed = true;
                modelSpace.AppendEntity(polyline);
                transaction.AddNewlyCreatedDBObject(polyline, true);
                samples.PolylineHandle = polyline.Handle.ToString();

                var text = new DBText { Position = new Point3d(0, 110, 0), Height = 3, TextString = "CADLookup smoke" };
                modelSpace.AppendEntity(text);
                transaction.AddNewlyCreatedDBObject(text, true);
                samples.TextHandle = text.Handle.ToString();

                blockTable.UpgradeOpen();
                var definition = new BlockTableRecord { Name = "CADLOOKUP_SMOKE_BLOCK" };
                blockTable.Add(definition);
                transaction.AddNewlyCreatedDBObject(definition, true);
                var attributeDefinition = new AttributeDefinition
                {
                    Position = Point3d.Origin,
                    Height = 3,
                    Tag = "SMOKE_TAG",
                    Prompt = "Smoke prompt",
                    TextString = "Default"
                };
                definition.AppendEntity(attributeDefinition);
                transaction.AddNewlyCreatedDBObject(attributeDefinition, true);
                var block = new BlockReference(new Point3d(100, 100, 0), definition.ObjectId);
                modelSpace.AppendEntity(block);
                transaction.AddNewlyCreatedDBObject(block, true);
                var attribute = new AttributeReference();
                attribute.SetAttributeFromBlock(attributeDefinition, block.BlockTransform);
                attribute.TextString = "Smoke value";
                block.AttributeCollection.AppendAttribute(attribute);
                transaction.AddNewlyCreatedDBObject(attribute, true);
                samples.BlockHandle = block.Handle.ToString();

                var erased = new Line(new Point3d(0, 150, 0), new Point3d(10, 150, 0));
                var erasedId = modelSpace.AppendEntity(erased);
                transaction.AddNewlyCreatedDBObject(erased, true);
                samples.ErasedHandle = erased.Handle.ToString();
                transaction.GetObject(erasedId, OpenMode.ForWrite).Erase();
                transaction.Commit();
            }
            return samples;
        }

        private sealed class SampleSet
        {
            public ObjectId LineId { get; set; }
            public string LineHandle { get; set; } = string.Empty;
            public string CircleHandle { get; set; } = string.Empty;
            public string PolylineHandle { get; set; } = string.Empty;
            public string TextHandle { get; set; } = string.Empty;
            public string BlockHandle { get; set; } = string.Empty;
            public string LayerHandle { get; set; } = string.Empty;
            public string ErasedHandle { get; set; } = string.Empty;
        }
    }
}
