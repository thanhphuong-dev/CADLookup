using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using CADLookup.Core.Contracts;
using CADLookup.Core.Domain;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace CADLookup.Host.AutoCAD
{
    public sealed class AutoCadHostAdapter : ICadHostAdapter
    {
        public CadHostInfo Host => new CadHostInfo("AutoCAD", AcadApp.Version.Major.ToString(), ".NET Framework 4.8",
            CadHostCapabilities.ObjectSelection | CadHostCapabilities.HandleLookup | CadHostCapabilities.ObjectRead |
            CadHostCapabilities.GeometrySummary | CadHostCapabilities.ExtendedData | CadHostCapabilities.ReflectedProperties |
            CadHostCapabilities.MethodCatalog | CadHostCapabilities.EventCatalog |
            CadHostCapabilities.RelationshipNavigation | CadHostCapabilities.ModelessInspector | CadHostCapabilities.JsonExport |
            CadHostCapabilities.TextInspection | CadHostCapabilities.BlockAttributeInspection);
        private Document ActiveDocument => AcadApp.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");

        public CadDocumentInfo? GetActiveDocument()
        {
            var document = AcadApp.DocumentManager.MdiActiveDocument;
            return document == null ? null : new CadDocumentInfo(document.Name, System.IO.Path.GetFileName(document.Name), document.Name);
        }

        public IReadOnlyList<CadObjectRef> SelectObjects(SelectionRequest request)
        {
            var document = ActiveDocument;
            var result = new List<CadObjectRef>();
            PromptSelectionResult? selection = request.UsePreselectedObjects
                ? document.Editor.SelectImplied()
                : null;
            if ((selection == null || selection.Status != PromptStatus.OK) && request.AllowInteractiveSelection)
                selection = document.Editor.GetSelection(new PromptSelectionOptions { MessageForAdding = "\nSelect objects to inspect: " });
            if (selection == null || selection.Status != PromptStatus.OK || selection.Value == null) return result;
            using (var transaction = document.Database.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (var id in selection.Value.GetObjectIds())
                {
                    var obj = transaction.GetObject(id, OpenMode.ForRead, true);
                    result.Add(ToReference(document, obj));
                }
            }
            return result;
        }

        public CadObjectRef? FindObjectByHandle(string handle)
        {
            try
            {
                var document = ActiveDocument;
                var id = document.Database.GetObjectId(false, new Handle(Convert.ToInt64(handle, 16)), 0);
                if (id.IsNull) return null;
                using (var tr = document.Database.TransactionManager.StartOpenCloseTransaction())
                    return ToReference(document, tr.GetObject(id, OpenMode.ForRead, true));
            }
            catch { return null; }
        }

        public CadObjectSnapshot ReadObject(CadObjectRef objectRef, ReadOptions options)
        {
            var document = ActiveDocument;
            if (!string.Equals(objectRef.DocumentId, document.Name, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The selected object belongs to a drawing that is no longer active.");
            var id = document.Database.GetObjectId(false, new Handle(Convert.ToInt64(objectRef.Handle, 16)), 0);
            using (var transaction = document.Database.TransactionManager.StartOpenCloseTransaction())
            {
                var root = transaction.GetObject(id, OpenMode.ForRead, true);
                var rootEntity = root as Entity;
                var ancestors = new List<object> { root };
                object value;
                try
                {
                    value = CADLookup.Core.Application.ReflectionMemberReader.ResolvePath(root, objectRef.MemberPath, ancestors,
                        (left, right) => left is DBObject leftDb && right is DBObject rightDb &&
                            !leftDb.ObjectId.IsNull && leftDb.ObjectId == rightDb.ObjectId);
                }
                catch (CADLookup.Core.Application.ReflectionMemberReader.CyclicMemberPathException ex)
                {
                    var name = objectRef.MemberPath.Split('/').LastOrDefault() ?? "Member";
                    return new CadObjectSnapshot(objectRef, objectRef.TypeName, rootEntity?.Layer ?? "—", "Available",
                        new[] { new CadProperty(name, objectRef.TypeName, ex.Message, "Reflection", PropertyStatus.CyclicReference) });
                }
                catch (Exception ex)
                {
                    var name = objectRef.MemberPath.Split('/').LastOrDefault() ?? "Member";
                    return new CadObjectSnapshot(objectRef, objectRef.TypeName, rootEntity?.Layer ?? "—", "Available",
                        new[] { new CadProperty(name, objectRef.TypeName, ex.Message, "Reflection", PropertyStatus.EvaluationError) });
                }
                var targetDbObject = value as DBObject;
                var entity = value as Entity;
                var properties = new List<CadProperty>();
                if (string.IsNullOrEmpty(objectRef.MemberPath))
                {
                    properties.Add(P("Handle", "Handle", root.Handle.ToString(), "Identity"));
                    properties.Add(P("ObjectId", "ObjectId", root.ObjectId.ToString(), "Identity"));
                    properties.Add(P("Runtime type", "Type", root.GetType().FullName ?? root.GetType().Name, "Runtime"));
                    properties.Add(P("DXF type", "String", root.GetRXClass().DxfName ?? "", "Runtime"));
                    properties.Add(P("Layer", "String", rootEntity?.Layer ?? "—", "Entity"));
                    properties.Add(P("Visible", "Boolean", rootEntity?.Visible.ToString() ?? "—", "Entity"));
                    properties.Add(P("Color", "Color", rootEntity?.Color.ToString() ?? "—", "Entity"));
                    properties.Add(P("Linetype", "String", rootEntity?.Linetype ?? "—", "Entity"));
                    properties.Add(P("Lineweight", "LineWeight", rootEntity?.LineWeight.ToString() ?? "—", "Entity"));
                    properties.Add(P("Transparency", "Transparency", rootEntity?.Transparency.ToString() ?? "—", "Entity"));
                    if (root is LayerTableRecord layer)
                    {
                        properties.Add(P("Name", "String", layer.Name, "Layer"));
                        properties.Add(P("Off", "Boolean", layer.IsOff.ToString(), "Layer"));
                        properties.Add(P("Frozen", "Boolean", layer.IsFrozen.ToString(), "Layer"));
                        properties.Add(P("Locked", "Boolean", layer.IsLocked.ToString(), "Layer"));
                        properties.Add(P("Plottable", "Boolean", layer.IsPlottable.ToString(), "Layer"));
                        properties.Add(P("Color", "Color", layer.Color.ToString(), "Layer"));
                        properties.Add(P("Linetype ID", "ObjectId", layer.LinetypeObjectId.ToString(), "Layer"));
                    }
                    else if (root is BlockTableRecord blockRecord)
                    {
                        properties.Add(P("Name", "String", blockRecord.Name, "Block definition"));
                        properties.Add(P("Is layout", "Boolean", blockRecord.IsLayout.ToString(), "Block definition"));
                        properties.Add(P("Is anonymous", "Boolean", blockRecord.IsAnonymous.ToString(), "Block definition"));
                        properties.Add(P("Origin", "Point3d", FormatPoint(blockRecord.Origin), "Block definition"));
                    }
                }
                if (string.IsNullOrEmpty(objectRef.MemberPath) && !root.OwnerId.IsNull && root.OwnerId != root.ObjectId)
                {
                    try
                    {
                        var owner = transaction.GetObject(root.OwnerId, OpenMode.ForRead, true);
                        var ownerReference = ToReference(document, owner);
                        properties.Add(new CadProperty("Owner", owner.GetType().Name, ownerReference.DisplayName, "Relationship", PropertyStatus.Available, ownerReference));
                    }
                    catch (Exception ex) { properties.Add(new CadProperty("Owner", "ObjectId", ex.Message, "Relationship", PropertyStatus.EvaluationError)); }
                }
                if (string.IsNullOrEmpty(objectRef.MemberPath) && rootEntity != null && !rootEntity.BlockId.IsNull)
                {
                    try
                    {
                        var block = transaction.GetObject(rootEntity.BlockId, OpenMode.ForRead, true);
                        var blockReference = ToReference(document, block);
                        properties.Add(new CadProperty("Block definition", block.GetType().Name, blockReference.DisplayName, "Relationship", PropertyStatus.Available, blockReference));
                    }
                    catch (Exception ex) { properties.Add(new CadProperty("Block definition", "ObjectId", ex.Message, "Relationship", PropertyStatus.EvaluationError)); }
                }
                var geometry = new List<GeometrySummary>();
                if (entity != null)
                {
                    try
                    {
                        var ext = entity.GeometricExtents;
                        geometry.Add(new GeometrySummary("Extents", FormatPoint(ext.MinPoint) + " → " + FormatPoint(ext.MaxPoint)));
                    }
                    catch { geometry.Add(new GeometrySummary("Extents", "Not available")); }
                }
                if (entity is Line line)
                {
                    properties.Add(P("Start point", "Point3d", FormatPoint(line.StartPoint), "Geometry"));
                    properties.Add(P("End point", "Point3d", FormatPoint(line.EndPoint), "Geometry"));
                    properties.Add(P("Length", "Double", line.Length.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Geometry"));
                }
                else if (entity is Circle circle)
                {
                    properties.Add(P("Center", "Point3d", FormatPoint(circle.Center), "Geometry"));
                    properties.Add(P("Radius", "Double", circle.Radius.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Geometry"));
                    properties.Add(P("Area", "Double", circle.Area.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Geometry"));
                }
                else if (entity is Polyline polyline)
                {
                    properties.Add(P("Vertex count", "Int32", polyline.NumberOfVertices.ToString(), "Geometry"));
                    properties.Add(P("Closed", "Boolean", polyline.Closed.ToString(), "Geometry"));
                    properties.Add(P("Length", "Double", polyline.Length.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Geometry"));
                }
                else if (entity is Arc arc)
                {
                    properties.Add(P("Center", "Point3d", FormatPoint(arc.Center), "Geometry"));
                    properties.Add(P("Radius", "Double", arc.Radius.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Geometry"));
                    properties.Add(P("Start angle", "Double", arc.StartAngle.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Geometry"));
                    properties.Add(P("End angle", "Double", arc.EndAngle.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Geometry"));
                    properties.Add(P("Length", "Double", arc.Length.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Geometry"));
                }
                else if (entity is Ellipse ellipse)
                {
                    properties.Add(P("Center", "Point3d", FormatPoint(ellipse.Center), "Geometry"));
                    properties.Add(P("Major axis", "Vector3d", ellipse.MajorAxis.ToString(), "Geometry"));
                    properties.Add(P("Minor radius", "Double", ellipse.MinorRadius.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Geometry"));
                    properties.Add(P("Start angle", "Double", ellipse.StartAngle.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Geometry"));
                    properties.Add(P("End angle", "Double", ellipse.EndAngle.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Geometry"));
                }
                else if (entity is DBPoint point)
                {
                    properties.Add(P("Position", "Point3d", FormatPoint(point.Position), "Geometry"));
                }
                else if (entity is Hatch hatch)
                {
                    properties.Add(P("Pattern", "String", hatch.PatternName, "Geometry"));
                    properties.Add(P("Pattern scale", "Double", hatch.PatternScale.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Geometry"));
                    properties.Add(P("Pattern angle", "Double", hatch.PatternAngle.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Geometry"));
                    properties.Add(P("Loop count", "Int32", hatch.NumberOfLoops.ToString(), "Geometry"));
                    try { properties.Add(P("Area", "Double", hatch.Area.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Geometry")); }
                    catch (Exception ex) { properties.Add(new CadProperty("Area", "Double", ex.Message, "Geometry", PropertyStatus.EvaluationError)); }
                }
                else if (entity is Dimension dimension)
                {
                    properties.Add(P("Measurement", "Double", dimension.Measurement.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Dimension"));
                    properties.Add(P("Dimension text", "String", dimension.DimensionText, "Dimension"));
                    properties.Add(P("Text position", "Point3d", FormatPoint(dimension.TextPosition), "Dimension"));
                }
                else if (entity is Spline spline)
                {
                    properties.Add(P("Degree", "Int32", spline.Degree.ToString(), "Geometry"));
                    properties.Add(P("Control points", "Int32", spline.NumControlPoints.ToString(), "Geometry"));
                    properties.Add(P("Closed", "Boolean", spline.Closed.ToString(), "Geometry"));
                    try { properties.Add(P("Length", "Double", spline.GetDistanceAtParameter(spline.EndParam).ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Geometry")); }
                    catch (Exception ex) { properties.Add(new CadProperty("Length", "Double", ex.Message, "Geometry", PropertyStatus.EvaluationError)); }
                }
                if (entity is DBText text)
                {
                    properties.Add(P("Text", "String", text.TextString, "Text"));
                    properties.Add(P("Position", "Point3d", FormatPoint(text.Position), "Text"));
                    properties.Add(P("Alignment point", "Point3d", FormatPoint(text.AlignmentPoint), "Text"));
                    properties.Add(P("Height", "Double", text.Height.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Text"));
                    properties.Add(P("Rotation", "Double", text.Rotation.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Text"));
                    properties.Add(P("Width factor", "Double", text.WidthFactor.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Text"));
                }
                else if (entity is MText mtext)
                {
                    properties.Add(P("Contents", "String", mtext.Contents, "Text"));
                    properties.Add(P("Plain text", "String", mtext.Text, "Text"));
                    properties.Add(P("Location", "Point3d", FormatPoint(mtext.Location), "Text"));
                    properties.Add(P("Height", "Double", mtext.TextHeight.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Text"));
                    properties.Add(P("Width", "Double", mtext.Width.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Text"));
                    properties.Add(P("Rotation", "Double", mtext.Rotation.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Text"));
                }
                if (entity is BlockReference blockRef)
                {
                    try
                    {
                        var definition = transaction.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead, true) as BlockTableRecord;
                        if (definition != null) properties.Add(P("Block name", "String", definition.Name, "Block"));
                    }
                    catch (Exception ex) { properties.Add(new CadProperty("Block name", "BlockTableRecord", ex.Message, "Block", PropertyStatus.EvaluationError)); }
                    properties.Add(P("Position", "Point3d", FormatPoint(blockRef.Position), "Block"));
                    properties.Add(P("Rotation", "Double", blockRef.Rotation.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Block"));
                    properties.Add(P("Scale", "Scale3d", blockRef.ScaleFactors.ToString(), "Block"));
                    try
                    {
                        var attributeCount = 0;
                        foreach (ObjectId attributeId in blockRef.AttributeCollection)
                        {
                            if (attributeCount++ >= options.MaxCollectionItems)
                            {
                                properties.Add(new CadProperty("Block attributes", "Collection", "Additional attributes omitted", "Block", PropertyStatus.Truncated));
                                break;
                            }
                            try
                            {
                                var attribute = transaction.GetObject(attributeId, OpenMode.ForRead, true) as AttributeReference;
                                if (attribute != null)
                                    properties.Add(new CadProperty("Attribute " + attribute.Tag, "AttributeReference", attribute.TextString,
                                        "Block", PropertyStatus.Available, ToReference(document, attribute)));
                            }
                            catch (Exception ex)
                            {
                                properties.Add(new CadProperty("Attribute " + attributeCount, "AttributeReference", ex.Message,
                                    "Block", PropertyStatus.EvaluationError));
                            }
                        }
                    }
                    catch (Exception ex) { properties.Add(new CadProperty("Attributes", "Collection", ex.Message, "Block", PropertyStatus.EvaluationError)); }
                }
                if (string.IsNullOrEmpty(objectRef.MemberPath) && options.IncludeExtendedData)
                {
                    try
                    {
                        var xdata = root.XData;
                        if (xdata != null)
                        {
                            var count = 0;
                            foreach (TypedValue typedValue in xdata)
                            {
                                if (count++ >= options.MaxExtendedDataItems) { properties.Add(new CadProperty("XData", "", "Further values omitted", "Extended data", PropertyStatus.Truncated)); break; }
                                properties.Add(P("XData " + count, typedValue.TypeCode.ToString(), Convert.ToString(typedValue.Value, System.Globalization.CultureInfo.InvariantCulture) ?? "null", "Extended data"));
                            }
                        }
                    }
                    catch (Exception ex) { properties.Add(new CadProperty("XData", "", ex.Message, "Extended data", PropertyStatus.EvaluationError)); }
                }
                if (properties.Count < options.MaxProperties)
                {
                    var reflectionOptions = new ReadOptions(options.IncludeExtendedData, options.MaxExtendedDataItems,
                        options.MaxProperties - properties.Count, options.MaxDepth, options.MaxCollectionItems);
                    var reflectedMembers = CADLookup.Core.Application.ReflectionMemberReader.Read(value, objectRef, reflectionOptions,
                        ancestors,
                        candidate => candidate is DBObject child && !child.ObjectId.IsNull ? ToReference(document, child) : null,
                        (left, right) => left is DBObject leftDb && right is DBObject rightDb &&
                            !leftDb.ObjectId.IsNull && leftDb.ObjectId == rightDb.ObjectId);
                    foreach (var member in reflectedMembers)
                    {
                        if (properties.Count >= options.MaxProperties)
                        {
                            properties.Add(new CadProperty("…", "", "Property limit reached", "Inspector", PropertyStatus.Truncated));
                            break;
                        }
                        properties.Add(member);
                    }
                }
                var state = root.IsErased ? "Erased" : targetDbObject != null && targetDbObject.IsErased ? "Erased" : "Available";
                return new CadObjectSnapshot(objectRef, value.GetType().FullName ?? value.GetType().Name,
                    entity?.Layer ?? rootEntity?.Layer ?? "—", state, properties, geometry);
            }
        }

        private static CadObjectRef ToReference(Document document, DBObject obj) =>
            new CadObjectRef("AutoCAD", document.Name, obj.Handle.ToString(), obj.ObjectId.ToString(), obj.GetType().Name,
                layer: (obj as Entity)?.Layer ?? string.Empty);
        private static CadProperty P(string name, string type, string value, string source) => new CadProperty(name, type, value, source);
        private static string FormatPoint(Point3d point) => string.Format(System.Globalization.CultureInfo.InvariantCulture, "X={0:G}, Y={1:G}, Z={2:G}", point.X, point.Y, point.Z);
    }
}
