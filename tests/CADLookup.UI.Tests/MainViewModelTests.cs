using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using CADLookup.Core.Application;
using CADLookup.Core.Contracts;
using CADLookup.Core.Domain;
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

        private sealed class FakeAdapter : ICadHostAdapter
        {
            private readonly CadObjectRef _first = new("Fake", "drawing.dwg", "A1", "native-A1", "Line", layer: "A-WALL");
            private readonly CadObjectRef _second = new("Fake", "drawing.dwg", "B2", "native-B2", "Circle", layer: "EQUIPMENT");
            public CadHostInfo Host => new("Fake", "1", "test");
            public CadDocumentInfo GetActiveDocument() => new("drawing.dwg", "drawing.dwg", "drawing.dwg");
            public IReadOnlyList<CadObjectRef> SelectObjects(SelectionRequest request) => new[] { _first, _second };
            public CadObjectRef? FindObjectByHandle(string handle) => handle == "B2" ? _second : null;
            public IReadOnlyList<CadObjectRef> SnoopDatabase() => new[] { _first, _second, new CadObjectRef("Fake", "drawing.dwg", "T1", "native-T1", "BlockTableRecord", layer: "*Model_Space") };
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
                    new[] { new GeometrySummary("Bounds", "0,0,0 - 1,1,0") });
            }
        }
    }
}


