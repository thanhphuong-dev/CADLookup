using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using CADLookup.Core.Application;
using CADLookup.Core.Contracts;
using CADLookup.Core.Domain;
using CADLookup.Infrastructure.Configuration;
using CADLookup.UI.Localization;
using CADLookup.UI.ViewModels;
using CADLookup.UI.Views;
using Xunit;

namespace CADLookup.UI.Tests
{
    public sealed class MainViewModelTests
    {
        [Fact]
        public void Search_and_source_filter_apply_to_visible_rows()
        {
            var host = new FakeAdapter();
            var viewModel = new MainViewModel(host, new ObjectInspector(host), new LocalizationService());

            viewModel.SearchText = "radius";

            Assert.Single(viewModel.Properties);
            Assert.Equal("Radius", viewModel.Properties[0].Name);
            viewModel.SearchText = string.Empty;
            viewModel.SelectedPropertySource = viewModel.Localization.LocalizeSource("Identity");
            Assert.All(viewModel.Properties, property => Assert.Equal(viewModel.Localization.LocalizeSource("Identity"), property.Source));
        }

        [Fact]
        public void Object_filter_matches_handle_type_and_layer()
        {
            var host = new FakeAdapter();
            var viewModel = new MainViewModel(host, new ObjectInspector(host), new LocalizationService());

            viewModel.ObjectFilter = "equipment";

            var item = Assert.Single(viewModel.VisibleObjects);
            Assert.Equal("B2", item.Handle);
        }

        [Fact]
        public void ViewModel_filters_support_multi_token_queries_without_false_positives()
        {
            var host = new FakeAdapter();
            var viewModel = new MainViewModel(host, new ObjectInspector(host), new LocalizationService());

            viewModel.SearchText = "radius 12.5";
            Assert.Single(viewModel.Properties);

            viewModel.SearchText = "radius missing";
            Assert.Empty(viewModel.Properties);

            viewModel.SearchText = string.Empty;
            viewModel.ObjectFilter = "line wall";
            Assert.Single(viewModel.ObjectTreeRoots);

            viewModel.ObjectFilter = "line equipment";
            Assert.Empty(viewModel.ObjectTreeRoots);
        }

        [Fact]
        public void Navigation_history_supports_back_and_forward()
        {
            var host = new FakeAdapter();
            var viewModel = new MainViewModel(host, new ObjectInspector(host), new LocalizationService());

            viewModel.SelectedProperty = viewModel.Properties.Single(property => property.Name == "Radius");
            viewModel.NavigateToSelectedProperty();

            Assert.Equal("B2", viewModel.SelectedHandle);
            Assert.Equal(2, viewModel.History.Count);
            Assert.True(viewModel.BackCommand.CanExecute(null));
            viewModel.BackCommand.Execute(null);
            Assert.Equal("A1", viewModel.SelectedHandle);
            viewModel.ForwardCommand.Execute(null);
            Assert.Equal("B2", viewModel.SelectedHandle);
        }

        [Fact]
        public void Handle_lookup_is_sent_through_the_host_command_callback()
        {
            var host = new FakeAdapter();
            string? requestedHandle = null;
            var viewModel = new MainViewModel(host, new ObjectInspector(host), new LocalizationService(),
                requestHandleLookup: handle => requestedHandle = handle);

            viewModel.HandleQuery = "  7F  ";
            viewModel.LookupHandleCommand.Execute(null);

            Assert.Equal("7F", requestedHandle);
        }

        [Fact]
        public void Main_view_loads_all_xaml_resources_on_an_sta_thread()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var host = new FakeAdapter();
                    var view = new MainView(new MainViewModel(host, new ObjectInspector(host), new LocalizationService()));
                    Assert.NotNull(view.Content);
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        [Fact]
        public void Search_dialog_loads_all_xaml_resources_on_an_sta_thread()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var host = new FakeAdapter(CadHostCapabilities.DatabaseSearch);
                    var dialog = new SearchDialog(new MainViewModel(host, new ObjectInspector(host), new LocalizationService()));
                    Assert.NotNull(dialog.Content);
                    dialog.Close();
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        [Fact]
        public void Reference_path_is_exposed_for_context_actions()
        {
            var host = new FakeAdapter();
            var viewModel = new MainViewModel(host, new ObjectInspector(host), new LocalizationService());
            viewModel.SelectedProperty = viewModel.Properties.Single(property => property.Name == "Radius");

            Assert.Equal(string.Empty, viewModel.SelectedProperty.ReferencePath);
            Assert.True(viewModel.CopyPathCommand.CanExecute(null));
            Assert.True(viewModel.OpenPropertyCommand.CanExecute(viewModel.SelectedProperty));
        }

        [Fact]
        public void Csv_export_quotes_values_and_keeps_member_path()
        {
            var reference = new CadObjectRef("Fake", "drawing.dwg", "A1", "native", "Line");
            var child = new CadObjectRef("Fake", "drawing.dwg", "A1", "native", "Double", "p:Length");
            var snapshot = new CadObjectSnapshot(reference, "Line", "A,Layer", "Available",
                new[] { new CadProperty("Caption", "String", "A \"quoted\" value", "Entity", reference: child) });
            var method = typeof(MainViewModel).GetMethod("SerializeCsv", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("CSV serializer was not found.");

            var csv = (string)method.Invoke(null, new object[] { snapshot })!;

            Assert.Contains("\"A,Layer\"", csv);
            Assert.Contains("\"A \"\"quoted\"\" value\"", csv);
            Assert.Contains("\"p:Length\"", csv);
        }

        [Fact]
        public void Tree_view_groups_objects_by_type_layer_and_flat()
        {
            var host = new FakeAdapter();
            var viewModel = new MainViewModel(host, new ObjectInspector(host), new LocalizationService());

            // By default grouped by Type
            viewModel.SelectedGroupBy = viewModel.Localization["GroupByType"];
            Assert.Equal(2, viewModel.ObjectTreeRoots.Count);
            Assert.Contains(viewModel.ObjectTreeRoots, root => root.Title == "Circle" && root.Children.Any(c => c.Title == "B2"));
            Assert.Contains(viewModel.ObjectTreeRoots, root => root.Title == "Line" && root.Children.Any(c => c.Title == "A1"));

            // Group by Layer
            viewModel.SelectedGroupBy = viewModel.Localization["GroupByLayer"];
            Assert.Equal(2, viewModel.ObjectTreeRoots.Count);
            Assert.Contains(viewModel.ObjectTreeRoots, root => root.Title == "A-WALL" && root.Children.Any(c => c.Subtitle == "A-WALL"));
            Assert.Contains(viewModel.ObjectTreeRoots, root => root.Title == "EQUIPMENT" && root.Children.Any(c => c.Subtitle == "EQUIPMENT"));

            // Flat List
            viewModel.SelectedGroupBy = viewModel.Localization["GroupByFlat"];
            Assert.Equal(2, viewModel.ObjectTreeRoots.Count);
            Assert.All(viewModel.ObjectTreeRoots, node => Assert.False(node.IsGroup));
        }

        [Fact]
        public void Theme_toggle_switches_theme_and_updates_properties()
        {
            var host = new FakeAdapter();
            var viewModel = new MainViewModel(host, new ObjectInspector(host), new LocalizationService());

            var initialTheme = viewModel.Theme;
            viewModel.ToggleThemeCommand.Execute(null);
            var newTheme = viewModel.Theme;

            Assert.NotEqual(initialTheme, newTheme);
            Assert.NotNull(viewModel.ThemeIcon);
            Assert.NotNull(viewModel.ThemeText);

            // Toggle back
            viewModel.ToggleThemeCommand.Execute(null);
            Assert.Equal(initialTheme, viewModel.Theme);
        }

        [Fact]
        public void Advanced_status_filter_and_clear_search_work()
        {
            var host = new FakeAdapter();
            var viewModel = new MainViewModel(host, new ObjectInspector(host), new LocalizationService());

            viewModel.SearchText = "Radius";
            Assert.True(viewModel.HasSearchText);
            Assert.True(viewModel.ClearSearchCommand.CanExecute(null));

            viewModel.ClearSearchCommand.Execute(null);
            Assert.Equal(string.Empty, viewModel.SearchText);
            Assert.False(viewModel.HasSearchText);

            viewModel.SelectedStatusFilter = viewModel.Localization["StatusEvaluated"];
            Assert.All(viewModel.Properties, p => Assert.Equal(viewModel.Localization.LocalizeStatus("Available"), p.Status));
        }

        [Fact]
        public void Language_switch_updates_text_and_preserves_group_and_status_filters()
        {
            var previousLanguage = UserSettingsStore.LoadLanguage();
            try
            {
                var host = new FakeAdapter();
                var viewModel = new MainViewModel(host, new ObjectInspector(host), new LocalizationService());
                viewModel.SelectedGroupBy = viewModel.Localization["GroupByLayer"];
                viewModel.SelectedStatusFilter = viewModel.Localization["StatusEvaluated"];
                string? changedTo = null;
                viewModel.LanguageChanged += language => changedTo = language;

                var nextLanguage = viewModel.Language == "vi" ? "en" : "vi";
                viewModel.SetLanguage(nextLanguage);

                Assert.Equal(nextLanguage, viewModel.Language);
                Assert.Equal(nextLanguage, changedTo);
                Assert.Equal(viewModel.Localization["GroupByLayer"], viewModel.SelectedGroupBy);
                Assert.Equal(viewModel.Localization["StatusEvaluated"], viewModel.SelectedStatusFilter);
                Assert.Contains(viewModel.SelectedStatusFilter, viewModel.StatusFilterOptions);
            }
            finally
            {
                UserSettingsStore.SaveLanguage(previousLanguage);
            }
        }

        [Fact]
        public void Snoop_database_loads_tables_into_tree_and_selects_first()
        {
            var host = new FakeAdapter();
            var viewModel = new MainViewModel(host, new ObjectInspector(host), new LocalizationService());

            viewModel.SnoopDatabaseCommand.Execute(null);

            Assert.Equal(3, viewModel.Objects.Count);
            Assert.Equal(3, viewModel.VisibleObjects.Count);
            Assert.Contains(viewModel.Objects, x => x.TypeName == "BlockTableRecord");
            Assert.True(viewModel.HasProperties);
            Assert.Equal("A1", viewModel.SelectedHandle);
        }

        [Fact]
        public void Zoom_and_select_route_the_current_reference_through_host_callbacks()
        {
            var host = new FakeAdapter(CadHostCapabilities.ZoomToObject | CadHostCapabilities.SelectInDrawing);
            CadObjectRef? zoomed = null;
            CadObjectRef? marked = null;
            var viewModel = new MainViewModel(host, new ObjectInspector(host), new LocalizationService(),
                requestZoomTo: reference => zoomed = reference,
                requestSelectInDrawing: reference => marked = reference);

            Assert.True(viewModel.SupportsZoomToObject);
            Assert.True(viewModel.SupportsSelectInDrawing);
            Assert.True(viewModel.SelectInDrawingCommand.CanExecute(null));
            Assert.True(viewModel.ZoomToObjectCommand.CanExecute(null));

            viewModel.SelectInDrawingCommand.Execute(null);
            viewModel.ZoomToObjectCommand.Execute(null);

            Assert.Equal("A1", marked?.Handle);
            Assert.Equal("A1", zoomed?.Handle);
        }

        [Fact]
        public void Zoom_command_prefers_property_reference_over_root()
        {
            var host = new FakeAdapter(CadHostCapabilities.ZoomToObject);
            CadObjectRef? zoomed = null;
            var viewModel = new MainViewModel(host, new ObjectInspector(host), new LocalizationService(),
                requestZoomTo: reference => zoomed = reference);

            viewModel.SelectedProperty = viewModel.Properties.Single(property => property.Name == "Radius");
            viewModel.ZoomToObjectCommand.Execute(viewModel.SelectedProperty);

            Assert.Equal("B2", zoomed?.Handle);
        }

        [Fact]
        public void Commands_report_unsupported_hosts_instead_of_throwing()
        {
            var host = new FakeAdapter();
            var viewModel = new MainViewModel(host, new ObjectInspector(host), new LocalizationService());

            Assert.False(viewModel.SupportsZoomToObject);
            viewModel.ZoomToObjectCommand.Execute(null);

            Assert.Equal(viewModel.Localization["FeatureUnsupported"], viewModel.StatusText);
        }

        [Fact]
        public void Database_search_routes_query_and_fills_results()
        {
            var host = new FakeAdapter(CadHostCapabilities.DatabaseSearch);
            string? requested = null;
            MainViewModel? viewModel = null;
            viewModel = new MainViewModel(host, new ObjectInspector(host), new LocalizationService(),
                requestDatabaseSearch: query =>
                {
                    requested = query;
                    viewModel!.ShowDatabaseSearchResults(host.SearchObjects(new SearchRequest(query)));
                });

            Assert.True(viewModel.SupportsDatabaseSearch);
            Assert.False(viewModel.SearchExecuteCommand.CanExecute(null));

            viewModel.DatabaseSearchQuery = "circle";
            Assert.True(viewModel.SearchExecuteCommand.CanExecute(null));
            viewModel.SearchExecuteCommand.Execute(null);

            Assert.Equal("circle", requested);
            Assert.Single(viewModel.SearchResults);
            Assert.Equal("B2", viewModel.SearchResults[0].Handle);
            Assert.True(viewModel.HasSearchResults);
        }

        [Fact]
        public void Toggle_highlight_routes_current_reference_and_clears()
        {
            var host = new FakeAdapter(CadHostCapabilities.TransientHighlight);
            var viewModel = new MainViewModel(host, new ObjectInspector(host), new LocalizationService());

            Assert.True(viewModel.SupportsTransientHighlight);
            Assert.False(viewModel.HighlightEnabled);

            viewModel.ToggleHighlightCommand.Execute(null);
            Assert.True(viewModel.HighlightEnabled);
            Assert.Equal("A1", host.Highlighted?.Handle);

            // Switching objects re-applies the highlight to the new object.
            viewModel.SelectedProperty = viewModel.Properties.Single(property => property.Name == "Radius");
            viewModel.NavigateToSelectedProperty();
            Assert.Equal("B2", viewModel.SelectedHandle);
            Assert.Equal("B2", host.Highlighted?.Handle);

            viewModel.ToggleHighlightCommand.Execute(null);
            Assert.False(viewModel.HighlightEnabled);
            Assert.Null(host.Highlighted);
        }

        [Fact]
        public void Reset_highlight_turns_the_toggle_off()
        {
            var host = new FakeAdapter(CadHostCapabilities.TransientHighlight);
            var viewModel = new MainViewModel(host, new ObjectInspector(host), new LocalizationService());

            viewModel.ToggleHighlightCommand.Execute(null);
            Assert.True(viewModel.HighlightEnabled);

            viewModel.ResetTransientHighlight();
            Assert.False(viewModel.HighlightEnabled);
            Assert.Null(host.Highlighted);
        }

        [Fact]
        public void Property_grouping_toggle_changes_the_view_groups()
        {
            var previousGrouping = UserSettingsStore.LoadPropertyGrouping();
            try
            {
                var host = new FakeAdapter();
                var viewModel = new MainViewModel(host, new ObjectInspector(host), new LocalizationService());

                var initial = viewModel.GroupPropertiesEnabled;
                viewModel.ToggleGroupPropertiesCommand.Execute(null);
                Assert.Equal(!initial, viewModel.GroupPropertiesEnabled);
                Assert.Equal(!initial, UserSettingsStore.LoadPropertyGrouping());

                if (viewModel.GroupPropertiesEnabled)
                {
                    Assert.NotNull(viewModel.PropertiesView.Groups);
                    Assert.True(viewModel.PropertiesView.Groups!.Count > 1,
                        "Expected properties to be grouped into more than one source.");
                }
                else
                {
                    Assert.True(viewModel.PropertiesView.Groups == null || viewModel.PropertiesView.Groups.Count == 0,
                        "Expected a flat property list when grouping is disabled.");
                }
            }
            finally
            {
                UserSettingsStore.SavePropertyGrouping(previousGrouping);
            }
        }

        [Fact]
        public void Tree_node_commands_resolve_the_node_reference()
        {
            var host = new FakeAdapter(CadHostCapabilities.ZoomToObject | CadHostCapabilities.SelectInDrawing);
            CadObjectRef? zoomed = null;
            var viewModel = new MainViewModel(host, new ObjectInspector(host), new LocalizationService(),
                requestZoomTo: reference => zoomed = reference);

            var node = new ObjectTreeNode("Circle", "B2", false, host.SearchObjects(new SearchRequest("circle"))[0],
                subtitle: "EQUIPMENT", icon: "◯");

            Assert.True(viewModel.ZoomToObjectCommand.CanExecute(node));
            Assert.True(viewModel.CopyHandleCommand.CanExecute(node));
            Assert.False(viewModel.CopyHandleCommand.CanExecute(new ObjectTreeNode("Group", "2", true)));

            viewModel.ZoomToObjectCommand.Execute(node);
            Assert.Equal("B2", zoomed?.Handle);
        }

        [Fact]
        public void Dashboard_visibility_tracks_the_current_object()
        {
            var host = new FakeAdapter();
            var viewModel = new MainViewModel(host, new ObjectInspector(host), new LocalizationService());
            Assert.True(viewModel.HasCurrentObject);

            viewModel.ClearForDocumentClose("drawing.dwg");
            Assert.False(viewModel.HasCurrentObject);
            Assert.False(viewModel.ExportCommand.CanExecute(null));
        }

        [Fact]
        public void Current_figures_come_from_the_snapshot_and_clear_with_it()
        {
            var host = new FakeAdapter();
            var viewModel = new MainViewModel(host, new ObjectInspector(host), new LocalizationService());

            Assert.Equal(2, viewModel.CurrentFigures.Count);
            Assert.Equal(GeometryFigure.PolylineKind, viewModel.CurrentFigures[0].Kind);
            Assert.Equal(4, viewModel.CurrentFigures[0].PointCount);
            Assert.True(viewModel.CurrentFigures[0].Closed);

            viewModel.ClearForDocumentClose("drawing.dwg");
            Assert.Empty(viewModel.CurrentFigures);
        }

        private sealed class FakeAdapter : ICadHostAdapter
        {
            private readonly CadObjectRef _first = new("Fake", "drawing.dwg", "A1", "native-A1", "Line", layer: "A-WALL");
            private readonly CadObjectRef _second = new("Fake", "drawing.dwg", "B2", "native-B2", "Circle", layer: "EQUIPMENT");

            public FakeAdapter(CadHostCapabilities capabilities = CadHostCapabilities.None)
            {
                Capabilities = capabilities;
            }

            public CadHostCapabilities Capabilities { get; }
            public CadHostInfo Host => new("Fake", "1", "test", Capabilities);
            public CadDocumentInfo GetActiveDocument() => new("drawing.dwg", "drawing.dwg", "drawing.dwg");
            public IReadOnlyList<CadObjectRef> SelectObjects(SelectionRequest request) => new[] { _first, _second };
            public CadObjectRef? FindObjectByHandle(string handle) => handle == "B2" ? _second : null;
            public IReadOnlyList<CadObjectRef> SnoopDatabase() => new[] { _first, _second, new CadObjectRef("Fake", "drawing.dwg", "T1", "native-T1", "BlockTableRecord", layer: "*Model_Space") };
            public IReadOnlyList<CadObjectRef> SearchObjects(SearchRequest request) =>
                string.IsNullOrWhiteSpace(request.Query) ? Array.Empty<CadObjectRef>() : new[] { _second };
            public List<CadObjectRef> ZoomRequests { get; } = new();
            public List<CadObjectRef> SelectRequests { get; } = new();
            public void ZoomToObject(CadObjectRef objectRef) => ZoomRequests.Add(objectRef);
            public void SelectInDrawing(CadObjectRef objectRef) => SelectRequests.Add(objectRef);
            public CadObjectRef? Highlighted { get; private set; }
            public void SetTransientHighlight(CadObjectRef? objectRef) => Highlighted = objectRef;
            public CadObjectSnapshot ReadObject(CadObjectRef objectRef, ReadOptions options)
            {
                var properties = objectRef.Handle == "A1"
                    ? new[]
                    {
                        new CadProperty("Handle", "String", "A1", "Identity"),
                        new CadProperty("Radius", "Double", "12.5", "Geometry", reference: _second)
                    }
                    : new[]
                    {
                        new CadProperty("Handle", "String", "B2", "Identity"),
                        new CadProperty("Layer", "String", "EQUIPMENT", "Entity")
                    };
                return new CadObjectSnapshot(objectRef, objectRef.TypeName, "0", "Available", properties,
                    new[] { new GeometrySummary("Bounds", "0,0,0 - 1,1,0") },
                    new[]
                    {
                        new GeometryFigure(GeometryFigure.PolylineKind, new double[] { 0, 0, 100, 0, 100, 40, 0, 40 }, closed: true),
                        new GeometryFigure(GeometryFigure.PointsKind, new double[] { 50, 20 })
                    });
            }
        }
    }
}


