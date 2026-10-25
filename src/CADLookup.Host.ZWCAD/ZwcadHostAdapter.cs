using System;
using System.Collections.Generic;
using System.Linq;
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.EditorInput;
using ZwSoft.ZwCAD.Geometry;
using CADLookup.Core.Application;
using CADLookup.Core.Contracts;
using CADLookup.Core.Domain;
using AcadApp = ZwSoft.ZwCAD.ApplicationServices.Core.Application;

namespace CADLookup.Host.ZWCAD
{
    public sealed class ZwcadHostAdapter : ICadHostAdapter
    {
        public bool LastSelectionWasCancelled { get; private set; }
        private ObjectId _highlightedId;
        private string? _highlightedDocumentName;

        public CadHostInfo Host => new CadHostInfo("ZWCAD", AcadApp.Version.Major.ToString(), ".NET Framework 4.8",
            CadHostCapabilities.ObjectSelection | CadHostCapabilities.HandleLookup | CadHostCapabilities.ObjectRead |
            CadHostCapabilities.GeometrySummary | CadHostCapabilities.ExtendedData | CadHostCapabilities.ReflectedProperties |
            CadHostCapabilities.MethodCatalog | CadHostCapabilities.EventCatalog |
            CadHostCapabilities.RelationshipNavigation | CadHostCapabilities.ModelessInspector | CadHostCapabilities.JsonExport |
            CadHostCapabilities.TextInspection | CadHostCapabilities.BlockAttributeInspection | CadHostCapabilities.DatabaseSnoop |
            CadHostCapabilities.ZoomToObject | CadHostCapabilities.SelectInDrawing | CadHostCapabilities.DatabaseSearch |
            CadHostCapabilities.TransientHighlight);
        private Document ActiveDocument => AcadApp.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");

        public CadDocumentInfo? GetActiveDocument()
        {
            var document = AcadApp.DocumentManager.MdiActiveDocument;
            return document == null ? null : new CadDocumentInfo(document.Name, System.IO.Path.GetFileName(document.Name), document.Name);
        }

        public IReadOnlyList<CadObjectRef> SelectObjects(SelectionRequest request)
        {
            LastSelectionWasCancelled = false;
            var document = ActiveDocument;
            var result = new List<CadObjectRef>();
            PromptSelectionResult? selection = null;
            if (request.UsePreselectedObjects)
            {
                var implied = document.Editor.SelectImplied();
                if (implied != null && implied.Status == PromptStatus.OK && implied.Value != null && implied.Value.Count > 0)
                {
                    selection = implied;
                }
            }
            if ((selection == null || selection.Status != PromptStatus.OK) && request.AllowInteractiveSelection)
            {
                var promptOptions = new PromptSelectionOptions
                {
                    MessageForAdding = "\nChọn đối tượng kiểm tra (hoặc Enter để tiếp tục): ",
                    RejectObjectsFromNonCurrentSpace = false,
                    AllowDuplicates = false
                };
                selection = document.Editor.GetSelection(promptOptions);
                LastSelectionWasCancelled = selection.Status == PromptStatus.Cancel;
            }
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

        public IReadOnlyList<CadObjectRef> SearchObjects(SearchRequest request)
        {
            var document = ActiveDocument;
            var result = new List<CadObjectRef>();
            if (string.IsNullOrWhiteSpace(request.Query)) return result;
            using (var transaction = document.Database.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (var spaceId in GetSearchableSpaces(document.Database, transaction))
                {
                    if (result.Count >= request.MaxResults) break;
                    var space = (BlockTableRecord)transaction.GetObject(spaceId, OpenMode.ForRead, true);
                    foreach (ObjectId id in space)
                    {
                        if (result.Count >= request.MaxResults) break;
                        DBObject obj;
                        try { obj = transaction.GetObject(id, OpenMode.ForRead, true); }
                        catch { continue; }
                        if (obj is Entity entity && MatchesQuery(request.Query, entity, transaction))
                            result.Add(ToReference(document, obj));
                    }
                }
            }
            return result;
        }

        private static IEnumerable<ObjectId> GetSearchableSpaces(Database database, Transaction transaction)
        {
            var spaces = new List<ObjectId>();
            if (!database.BlockTableId.IsNull)
            {
                var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead, true);
                spaces.Add(blockTable[BlockTableRecord.ModelSpace]);
            }
            if (!database.CurrentSpaceId.IsNull && !spaces.Contains(database.CurrentSpaceId))
                spaces.Add(database.CurrentSpaceId);
            return spaces;
        }

        private static bool MatchesQuery(string query, Entity entity, Transaction transaction)
        {
            var fields = new List<string?>
            {
                entity.Handle.ToString(),
                entity.GetType().Name,
                entity.GetRXClass().DxfName,
                entity.Layer
            };
            switch (entity)
            {
                case DBText text:
                    fields.Add(text.TextString);
                    break;
                case MText mtext:
                    fields.Add(mtext.Text);
                    fields.Add(mtext.Contents);
                    break;
                case BlockReference blockReference:
                    fields.Add(blockReference.Name);
                    try
                    {
                        if (transaction.GetObject(blockReference.BlockTableRecord, OpenMode.ForRead, true) is BlockTableRecord definition)
                            fields.Add(definition.Name);
                    }
                    catch { }
                    foreach (ObjectId attributeId in blockReference.AttributeCollection)
                    {
                        try
                        {
                            if (transaction.GetObject(attributeId, OpenMode.ForRead, true) is AttributeReference attribute)
                            {
                                fields.Add(attribute.Tag);
                                fields.Add(attribute.TextString);
                            }
                        }
                        catch { }
                    }
                    break;
            }
            return TextMatcher.Matches(query, fields.ToArray());
        }

        public void ZoomToObject(CadObjectRef objectRef)
        {
            var document = ActiveDocument;
            if (!string.Equals(objectRef.DocumentId, document.Name, StringComparison.OrdinalIgnoreCase)) return;
            var id = document.Database.GetObjectId(false, new Handle(Convert.ToInt64(objectRef.Handle, 16)), 0);
            if (id.IsNull) return;
            using (var transaction = document.Database.TransactionManager.StartOpenCloseTransaction())
            {
                if (!(transaction.GetObject(id, OpenMode.ForRead, true) is Entity entity)) return;
                Extents3d extents;
                try { extents = entity.GeometricExtents; }
                catch { return; }
                var editor = document.Editor;
                var width = extents.MaxPoint.X - extents.MinPoint.X;
                var height = extents.MaxPoint.Y - extents.MinPoint.Y;
                var margin = Math.Max(Math.Max(width, height) * 0.1, 1e-4);
                extents.ExpandBy(new Vector3d(margin, margin, margin));
                extents.TransformBy(editor.CurrentUserCoordinateSystem.Inverse());
                var view = editor.GetCurrentView();
                view.Width = extents.MaxPoint.X - extents.MinPoint.X;
                view.Height = extents.MaxPoint.Y - extents.MinPoint.Y;
                view.CenterPoint = new Point2d(
                    (extents.MinPoint.X + extents.MaxPoint.X) / 2.0,
                    (extents.MinPoint.Y + extents.MaxPoint.Y) / 2.0);
                editor.SetCurrentView(view);
            }
        }

        public void SelectInDrawing(CadObjectRef objectRef)
        {
            var document = ActiveDocument;
            if (!string.Equals(objectRef.DocumentId, document.Name, StringComparison.OrdinalIgnoreCase)) return;
            var id = document.Database.GetObjectId(false, new Handle(Convert.ToInt64(objectRef.Handle, 16)), 0);
            if (id.IsNull) return;
            document.Editor.SetImpliedSelection(new[] { id });
        }

        public void SetTransientHighlight(CadObjectRef? objectRef)
        {
            UnhighlightPrevious();
            if (objectRef == null) return;
            var document = ActiveDocument;
            if (!string.Equals(objectRef.DocumentId, document.Name, StringComparison.OrdinalIgnoreCase)) return;
            var id = document.Database.GetObjectId(false, new Handle(Convert.ToInt64(objectRef.Handle, 16)), 0);
            if (id.IsNull) return;
            using (var transaction = document.Database.TransactionManager.StartOpenCloseTransaction())
            {
                if (!(transaction.GetObject(id, OpenMode.ForRead, true) is Entity entity)) return;
                if (!ApplyHighlight(entity, highlight: true)) return;
                _highlightedDocumentName = document.Name;
                _highlightedId = id;
            }
            try { document.Editor.UpdateScreen(); }
            catch { }
        }

        private void UnhighlightPrevious()
        {
            var id = _highlightedId;
            var documentName = _highlightedDocumentName;
            _highlightedId = ObjectId.Null;
            _highlightedDocumentName = null;
            if (id.IsNull || documentName == null) return;
            try
            {
                var document = ActiveDocument;
                if (!string.Equals(documentName, document.Name, StringComparison.OrdinalIgnoreCase)) return;
                using (var transaction = document.Database.TransactionManager.StartOpenCloseTransaction())
                {
                    if (transaction.GetObject(id, OpenMode.ForRead, true) is Entity entity)
                        ApplyHighlight(entity, highlight: false);
                }
            }
            catch { }
        }

        private static bool ApplyHighlight(Entity entity, bool highlight)
        {
            try
            {
                if (highlight) entity.Highlight(); else entity.Unhighlight();
                return true;
            }
            catch
            {
                // Some hosts require write access for the highlight graphics flag.
                try
                {
                    entity.UpgradeOpen();
                    if (highlight) entity.Highlight(); else entity.Unhighlight();
                    return true;
                }
                catch { return false; }
            }
        }

        public IReadOnlyList<CadObjectRef> SnoopDatabase()
        {
            var document = ActiveDocument;
            var result = new List<CadObjectRef>();
            using (var tr = document.Database.TransactionManager.StartOpenCloseTransaction())
            {
                var db = document.Database;

                if (!db.BlockTableId.IsNull)
                {
                    var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead, true);
                    foreach (ObjectId btrId in blockTable)
                    {
                        var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead, true);
                        result.Add(new CadObjectRef(Host.DisplayName, document.Name, btr.Handle.ToString(), btr.ObjectId.ToString(),
                            "BlockTableRecord", layer: btr.Name));
                    }
                }

                if (!db.LayerTableId.IsNull)
                {
                    var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead, true);
                    foreach (ObjectId ltrId in layerTable)
                    {
                        var ltr = (LayerTableRecord)tr.GetObject(ltrId, OpenMode.ForRead, true);
                        result.Add(new CadObjectRef(Host.DisplayName, document.Name, ltr.Handle.ToString(), ltr.ObjectId.ToString(),
                            "LayerTableRecord", layer: ltr.Name));
                    }
                }

                if (!db.TextStyleTableId.IsNull)
                {
                    var textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead, true);
                    foreach (ObjectId ttrId in textStyleTable)
                    {
                        var ttr = (TextStyleTableRecord)tr.GetObject(ttrId, OpenMode.ForRead, true);
                        result.Add(new CadObjectRef(Host.DisplayName, document.Name, ttr.Handle.ToString(), ttr.ObjectId.ToString(),
                            "TextStyleTableRecord", layer: ttr.Name));
                    }
                }

                if (!db.LinetypeTableId.IsNull)
                {
                    var linetypeTable = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead, true);
                    foreach (ObjectId ltrId in linetypeTable)
                    {
                        var ltr = (LinetypeTableRecord)tr.GetObject(ltrId, OpenMode.ForRead, true);
                        result.Add(new CadObjectRef(Host.DisplayName, document.Name, ltr.Handle.ToString(), ltr.ObjectId.ToString(),
                            "LinetypeTableRecord", layer: ltr.Name));
                    }
                }

                if (!db.DimStyleTableId.IsNull)
                {
                    var dimStyleTable = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead, true);
                    foreach (ObjectId dtrId in dimStyleTable)
                    {
                        var dtr = (DimStyleTableRecord)tr.GetObject(dtrId, OpenMode.ForRead, true);
                        result.Add(new CadObjectRef(Host.DisplayName, document.Name, dtr.Handle.ToString(), dtr.ObjectId.ToString(),
                            "DimStyleTableRecord", layer: dtr.Name));
                    }
                }

                if (!db.RegAppTableId.IsNull)
                {
                    var regAppTable = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead, true);
                    foreach (ObjectId rtrId in regAppTable)
                    {
                        var rtr = (RegAppTableRecord)tr.GetObject(rtrId, OpenMode.ForRead, true);
                        result.Add(new CadObjectRef(Host.DisplayName, document.Name, rtr.Handle.ToString(), rtr.ObjectId.ToString(),
                            "RegAppTableRecord", layer: rtr.Name));
                    }
                }

                if (!db.NamedObjectsDictionaryId.IsNull)
                {
                    var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead, true);
                    result.Add(new CadObjectRef(Host.DisplayName, document.Name, nod.Handle.ToString(), nod.ObjectId.ToString(),
                        "DBDictionary", layer: "NamedObjectsDictionary"));
                    foreach (DBDictionaryEntry entry in nod)
                    {
                        try
                        {
                            var entryObj = tr.GetObject(entry.Value, OpenMode.ForRead, true);
                            result.Add(new CadObjectRef(Host.DisplayName, document.Name, entryObj.Handle.ToString(), entryObj.ObjectId.ToString(),
                                entryObj.GetType().Name, layer: entry.Key));
                        }
                        catch { }
                    }
                }

                if (!db.UcsTableId.IsNull)
                {
                    var ucsTable = (UcsTable)tr.GetObject(db.UcsTableId, OpenMode.ForRead, true);
                    foreach (ObjectId id in ucsTable)
                    {
                        var ucs = (UcsTableRecord)tr.GetObject(id, OpenMode.ForRead, true);
                        result.Add(new CadObjectRef(Host.DisplayName, document.Name, ucs.Handle.ToString(), ucs.ObjectId.ToString(),
                            "UcsTableRecord", layer: ucs.Name));
                    }
                }
            }
            return result;
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
                    else if (root is TextStyleTableRecord textStyle)
                    {
                        properties.Add(P("Name", "String", textStyle.Name, "Text style"));
                        properties.Add(P("Font", "String", textStyle.Font.TypeFace, "Text style"));
                        properties.Add(P("Text size", "Double", textStyle.TextSize.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Text style"));
                        properties.Add(P("X scale", "Double", textStyle.XScale.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Text style"));
                        properties.Add(P("Obliquing angle", "Double", textStyle.ObliquingAngle.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Text style"));
                        properties.Add(P("File name", "String", textStyle.FileName, "Text style"));
                        properties.Add(P("Big font file", "String", textStyle.BigFontFileName, "Text style"));
                    }
                    else if (root is LinetypeTableRecord linetype)
                    {
                        properties.Add(P("Name", "String", linetype.Name, "Linetype"));
                        properties.Add(P("Comments", "String", linetype.Comments, "Linetype"));
                        properties.Add(P("Pattern length", "Double", linetype.PatternLength.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Linetype"));
                        properties.Add(P("Dashes count", "Int32", linetype.NumDashes.ToString(), "Linetype"));
                    }
                    else if (root is DimStyleTableRecord dimStyle)
                    {
                        properties.Add(P("Name", "String", dimStyle.Name, "Dimension style"));
                        properties.Add(P("Dim scale", "Double", dimStyle.Dimscale.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Dimension style"));
                        properties.Add(P("Arrow size", "Double", dimStyle.Dimasz.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Dimension style"));
                        properties.Add(P("Text height", "Double", dimStyle.Dimtxt.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "Dimension style"));
                    }
                    else if (root is DBDictionary dict)
                    {
                        properties.Add(P("Entries count", "Int32", dict.Count.ToString(), "Dictionary"));
                        properties.Add(P("Treat elements as hard", "Boolean", dict.TreatElementsAsHard.ToString(), "Dictionary"));
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
                    entity?.Layer ?? rootEntity?.Layer ?? "—", state, properties, geometry, ExtractFigures(entity));
            }
        }

        private static CadObjectRef ToReference(Document document, DBObject obj) =>
            new CadObjectRef("ZWCAD", document.Name, obj.Handle.ToString(), obj.ObjectId.ToString(), obj.GetType().Name,
                layer: (obj as Entity)?.Layer ?? string.Empty);

        /// <summary>Extracts host-neutral drawable figures: exact curves where cheap, else the extents box.</summary>
        private static List<GeometryFigure> ExtractFigures(Entity? entity)
        {
            var figures = new List<GeometryFigure>();
            if (entity == null) return figures;
            try
            {
                switch (entity)
                {
                    case Line line:
                        figures.Add(PolylineFigure(new[] { line.StartPoint, line.EndPoint }, closed: false));
                        break;
                    case Circle circle:
                        figures.Add(ApproximateArc(circle.Center, circle.Radius, 0.0, Math.PI * 2));
                        break;
                    case Arc arc:
                        figures.Add(ApproximateArc(arc.Center, arc.Radius, arc.StartAngle, arc.EndAngle));
                        break;
                    case Ellipse ellipse:
                        figures.Add(ApproximateEllipse(ellipse));
                        break;
                    case Polyline polyline when polyline.NumberOfVertices >= 2:
                        {
                            var points = new List<Point3d>(polyline.NumberOfVertices);
                            for (var i = 0; i < polyline.NumberOfVertices; i++)
                                points.Add(polyline.GetPoint3dAt(i));
                            figures.Add(PolylineFigure(points, polyline.Closed));
                            break;
                        }
                    case Spline spline:
                        {
                            var points = new List<Point3d>(49);
                            var start = spline.StartParam;
                            var end = spline.EndParam;
                            for (var i = 0; i <= 48; i++)
                                points.Add(spline.GetPointAtParameter(start + (end - start) * i / 48.0));
                            figures.Add(PolylineFigure(points, spline.Closed));
                            break;
                        }
                    case DBPoint point:
                        figures.Add(new GeometryFigure(GeometryFigure.PointsKind, new[] { point.Position.X, point.Position.Y }));
                        break;
                }
            }
            catch { }
            if (figures.Count == 0)
            {
                try
                {
                    var extents = entity.GeometricExtents;
                    figures.Add(PolylineFigure(new[]
                    {
                        new Point3d(extents.MinPoint.X, extents.MinPoint.Y, 0),
                        new Point3d(extents.MaxPoint.X, extents.MinPoint.Y, 0),
                        new Point3d(extents.MaxPoint.X, extents.MaxPoint.Y, 0),
                        new Point3d(extents.MinPoint.X, extents.MaxPoint.Y, 0)
                    }, closed: true));
                }
                catch { }
            }
            return figures;
        }

        private static GeometryFigure PolylineFigure(IReadOnlyList<Point3d> points, bool closed)
        {
            var coordinates = new List<double>(points.Count * 2);
            foreach (var point in points)
            {
                coordinates.Add(point.X);
                coordinates.Add(point.Y);
            }
            return new GeometryFigure(GeometryFigure.PolylineKind, coordinates, closed);
        }

        private static GeometryFigure ApproximateArc(Point3d center, double radius, double startAngle, double endAngle)
        {
            var span = endAngle - startAngle;
            if (span <= 0) span += Math.PI * 2;
            const int steps = 48;
            var points = new List<Point3d>(steps + 1);
            for (var i = 0; i <= steps; i++)
            {
                var angle = startAngle + span * i / steps;
                points.Add(new Point3d(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle), 0));
            }
            return new GeometryFigure(GeometryFigure.PolylineKind, Flatten(points), span >= Math.PI * 2 - 1e-9);
        }

        private static GeometryFigure ApproximateEllipse(Ellipse ellipse)
        {
            var start = ellipse.StartAngle;
            var end = ellipse.EndAngle;
            var span = end - start;
            if (span <= 0) span += Math.PI * 2;
            const int steps = 48;
            var points = new List<Point3d>(steps + 1);
            for (var i = 0; i <= steps; i++)
            {
                var t = start + span * i / steps;
                points.Add(new Point3d(
                    ellipse.Center.X + ellipse.MajorAxis.X * Math.Cos(t) + ellipse.MinorAxis.X * Math.Sin(t),
                    ellipse.Center.Y + ellipse.MajorAxis.Y * Math.Cos(t) + ellipse.MinorAxis.Y * Math.Sin(t), 0));
            }
            return new GeometryFigure(GeometryFigure.PolylineKind, Flatten(points), span >= Math.PI * 2 - 1e-9);
        }

        private static List<double> Flatten(IReadOnlyList<Point3d> points)
        {
            var coordinates = new List<double>(points.Count * 2);
            foreach (var point in points)
            {
                coordinates.Add(point.X);
                coordinates.Add(point.Y);
            }
            return coordinates;
        }
        private static CadProperty P(string name, string type, string value, string source) => new CadProperty(name, type, value, source);
        private static string FormatPoint(Point3d point) => string.Format(System.Globalization.CultureInfo.InvariantCulture, "X={0:G}, Y={1:G}, Z={2:G}", point.X, point.Y, point.Z);
    }
}
