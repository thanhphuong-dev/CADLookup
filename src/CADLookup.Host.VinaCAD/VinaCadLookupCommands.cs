using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CADLookup.Core.Application;
using CADLookup.Core.Contracts;
using CADLookup.UI.Localization;
using CADLookup.UI.ViewModels;
using CADLookup.UI.Views;
using Prima.VinaCAD.ApplicationServices;
using Prima.VinaCAD.EditorInput;
using Teigha.Runtime;
using Teigha.Windows;
using VinaApp = Prima.VinaCAD.ApplicationServices.Application;

namespace CADLookup.Host.VinaCAD
{
    public sealed class VinaCadLookupCommands : IExtensionApplication
    {
        private static InspectorWindow? _window;
        private static MainViewModel? _viewModel;
        private static VinaCadHostAdapter? _hostAdapter;
        private static CadObjectRef? _requestedReference;
        private static string? _requestedHandle;
        private static CadObjectRef? _requestedZoomReference;
        private static CadObjectRef? _requestedMarkReference;
        private static string? _requestedSearchQuery;
        private static CadObjectRef? _requestedHighlightReference;

        public void Initialize()
        {
            AssemblyLoadContext.Default.Resolving += ResolveBundleAssembly;
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null)
                dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(EnsureRibbon));
        }

        public void Terminate()
        {
            AssemblyLoadContext.Default.Resolving -= ResolveBundleAssembly;
            _window?.Close();
            _window = null;
            _viewModel = null;
            _hostAdapter = null;
        }

        private static Assembly? ResolveBundleAssembly(AssemblyLoadContext context, AssemblyName name)
        {
            var directory = Path.GetDirectoryName(typeof(VinaCadLookupCommands).Assembly.Location);
            if (string.IsNullOrEmpty(directory)) return null;
            var candidate = Path.Combine(directory, name.Name + ".dll");
            return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
        }

        private static void EnsureRibbon()
        {
            try
            {
                var ribbon = RibbonServices.RibbonPaletteSet?.RibbonControl;
                if (ribbon?.Tabs == null) return;

                const string tabId = "CADLOOKUP_VINACAD_TAB";
                var tab = ribbon.Tabs.FirstOrDefault(item => item.Id == tabId);
                if (tab == null)
                {
                    tab = new RibbonTab { Id = tabId, Name = "CADLookup", Title = "CADLookup" };
                    ribbon.Tabs.Add(tab);
                }

                const string panelId = "CADLOOKUP_VINACAD_PANEL";
                var panel = tab.Panels.FirstOrDefault(item => item.Source?.Id == panelId);
                if (panel == null)
                {
                    var source = new RibbonPanelSource { Id = panelId, Name = "CADLookup", Title = "Object Inspector" };
                    panel = new RibbonPanel { Source = source };
                    tab.Panels.Add(panel);
                }

                var panelSource = panel.Source;
                if (panelSource == null) return;
                AddRibbonButton(panelSource, "CADLOOKUP_VINACAD_OPEN", RibbonIcon.Inspect,
                    Color.FromRgb(37, 99, 235), "CADLOOKUP");
                AddRibbonButton(panelSource, "CADLOOKUP_VINACAD_SELECT", RibbonIcon.Select,
                    Color.FromRgb(16, 185, 129), "CADLOOKUPSELECT");
                AddRibbonButton(panelSource, "CADLOOKUP_VINACAD_DATABASE", RibbonIcon.Database,
                    Color.FromRgb(139, 92, 246), "CADLOOKUPSNOOPDB");
                AddRibbonButton(panelSource, "CADLOOKUP_VINACAD_SEARCH", RibbonIcon.Search,
                    Color.FromRgb(245, 158, 11), "CADLOOKUPFIND");
                AddRibbonButton(panelSource, "CADLOOKUP_VINACAD_REFRESH", RibbonIcon.Refresh,
                    Color.FromRgb(245, 158, 11), "CADLOOKUPREFRESH");
                AddRibbonButton(panelSource, "CADLOOKUP_VINACAD_LANGUAGE", RibbonIcon.Language,
                    Color.FromRgb(14, 165, 233), "CADLOOKUPLANG");
                UpdateRibbonLanguage();
            }
            catch
            {
                // Ribbon services can be unavailable while VinaCAD is initializing.
            }
        }

        private static void AddRibbonButton(RibbonPanelSource source, string id, RibbonIcon icon, Color color, string command)
        {
            var button = source.Items.OfType<RibbonCommandButton>().FirstOrDefault(item => item.Id == id);
            if (button == null)
            {
                button = new RibbonCommandButton { Id = id, CommandHandler = new RibbonCommand(command) };
                source.Items.Add(button);
            }

            button.ShowText = true;
            button.ShowImage = true;
            button.Size = RibbonItemSize.Large;
            button.Orientation = Orientation.Vertical;
            button.LargeImage = CreateIcon(icon, color, 32);
            button.Image = CreateIcon(icon, color, 16);
        }

        private static void UpdateRibbonLanguage()
        {
            var ribbon = RibbonServices.RibbonPaletteSet?.RibbonControl;
            var tab = ribbon?.Tabs?.FirstOrDefault(item => item.Id == "CADLOOKUP_VINACAD_TAB");
            var source = tab?.Panels.FirstOrDefault(item => item.Source?.Id == "CADLOOKUP_VINACAD_PANEL")?.Source;
            if (source == null) return;

            var localization = new LocalizationService();
            source.Title = localization["RibbonPanelTitle"];
            SetButtonText(source, "CADLOOKUP_VINACAD_OPEN", localization["RibbonInspect"], localization["RibbonInspectHint"]);
            SetButtonText(source, "CADLOOKUP_VINACAD_SELECT", localization["RibbonSelect"], localization["RibbonSelectHint"]);
            SetButtonText(source, "CADLOOKUP_VINACAD_DATABASE", localization["RibbonSnoop"], localization["RibbonSnoopHint"]);
            SetButtonText(source, "CADLOOKUP_VINACAD_SEARCH", localization["RibbonSearch"], localization["RibbonSearchHint"]);
            SetButtonText(source, "CADLOOKUP_VINACAD_REFRESH", localization["RibbonRefresh"], localization["RibbonRefreshHint"]);
            SetButtonText(source, "CADLOOKUP_VINACAD_LANGUAGE", localization["Language"], localization["RibbonLanguageHint"]);
        }

        private static void SetButtonText(RibbonPanelSource source, string id, string text, string toolTip)
        {
            var button = source.Items.OfType<RibbonCommandButton>().FirstOrDefault(item => item.Id == id);
            if (button == null) return;
            button.Text = text;
            button.ToolTip = toolTip;
        }

        private static ImageSource CreateIcon(RibbonIcon icon, Color color, int size)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            var geometry = new GeometryGroup();
            geometry.Children.Add(Geometry.Parse(IconGeometry(icon)));
            var scale = size * (1.5 / 32.0);
            geometry.Transform = new MatrixTransform(scale, 0, 0, scale, size / 32.0, size / 32.0);
            var pen = new Pen(brush, size * (2.2 / 32.0))
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            pen.Freeze();
            var group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing(null, pen, geometry));
            var image = new DrawingImage(group);
            image.Freeze();
            return image;
        }

        private static string IconGeometry(RibbonIcon icon)
        {
            switch (icon)
            {
                case RibbonIcon.Inspect: return "M4,2L12,2 12,8 M4,2L4,16 10,16 M6,5L10,5 M6,8L9,8 M15.2,10.8A3.4,3.4,0,1,1,10.8,15.2A3.4,3.4,0,1,1,15.2,10.8 M17.8,17.8L14.6,14.6";
                case RibbonIcon.Select: return "M3,2L8,19 11,12 18,9Z M11,12L16,18";
                case RibbonIcon.Database: return "M3,4C3,1 17,1 17,4C17,7 3,7 3,4 M3,4L3,16C3,19 17,19 17,16L17,4 M3,10C3,13 17,13 17,10";
                case RibbonIcon.Refresh: return "M16,7A7,7 0 1 0 17,12 M16,3L16,8 11,8";
                case RibbonIcon.Search: return "M3,8.2A5.2,5.2 0 1 0 13.4,8.2A5.2,5.2 0 1 0 3,8.2 M12,12L16.5,16.5";
                default: return "M10,2A8,8 0 1 0 10,18A8,8 0 1 0 10,2 M2,10L18,10 M10,2C5,6 5,14 10,18 M10,2C15,6 15,14 10,18";
            }
        }

        [CommandMethod("CADLOOKUP", CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void ShowCadLookup()
        {
            EnsureRibbon();
            EnsureViewModel();
            var document = VinaApp.DocumentManager.MdiActiveDocument;
            var implied = document?.Editor.SelectImplied();
            if (implied?.Status == PromptStatus.OK && implied.Value?.Count > 0)
                _viewModel!.UpdateSelectionFromHost(false);
            ShowInspectorWindow();
        }

        [CommandMethod("CADLOOKUPSELECT")]
        public void SelectForCadLookup()
        {
            EnsureViewModel();
            _viewModel!.UpdateSelectionFromHost(true);
            if (_hostAdapter?.LastSelectionWasCancelled == true) return;
            ShowInspectorWindow();
        }

        [CommandMethod("CADLOOKUPSNOOPDB")]
        [CommandMethod("CADLOOKUPDB")]
        public void SnoopDatabaseForCadLookup()
        {
            EnsureRibbon();
            EnsureViewModel();
            ShowInspectorWindow();
            _viewModel!.SnoopDatabaseFromHost();
        }

        [CommandMethod("CADLOOKUPREFRESH")]
        public void RefreshForCadLookup()
        {
            EnsureRibbon();
            // Sent on document activation; it must not open the inspector as a startup side effect.
            if (_viewModel == null) return;
            var reference = _viewModel.CurrentReference;
            var document = VinaApp.DocumentManager.MdiActiveDocument;
            if (reference != null && document != null &&
                string.Equals(reference.DocumentId, document.Name, StringComparison.OrdinalIgnoreCase))
                _viewModel.SelectObject(reference, false);
            else
                _viewModel.UpdateSelectionFromHost(false);
        }

        [CommandMethod("CADLOOKUPINSPECT")]
        public void InspectForCadLookup()
        {
            if (_requestedReference != null)
            {
                _viewModel?.SelectObject(_requestedReference);
                _requestedReference = null;
            }
            else if (_requestedHandle != null)
                _viewModel?.InspectFromHost(_requestedHandle);
            _requestedHandle = null;
        }

        [CommandMethod("CADLOOKUPZOOM")]
        public void ZoomToInspectedObject()
        {
            if (_requestedZoomReference == null) return;
            var reference = _requestedZoomReference;
            _requestedZoomReference = null;
            try
            {
                _hostAdapter?.ZoomToObject(reference);
                var localization = new LocalizationService();
                VinaApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n" + localization["ZoomedToObject"]);
                if (_viewModel != null) _viewModel.StatusText = localization["ZoomedToObject"];
            }
            catch (System.Exception ex)
            {
                VinaApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n" + ex.Message);
            }
        }

        [CommandMethod("CADLOOKUPMARK")]
        public void MarkInspectedObject()
        {
            if (_requestedMarkReference == null) return;
            var reference = _requestedMarkReference;
            _requestedMarkReference = null;
            try
            {
                _hostAdapter?.SelectInDrawing(reference);
                var localization = new LocalizationService();
                VinaApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n" + localization["MarkedInDrawing"]);
                if (_viewModel != null) _viewModel.StatusText = localization["MarkedInDrawing"];
            }
            catch (System.Exception ex)
            {
                VinaApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n" + ex.Message);
            }
        }

        [CommandMethod("CADLOOKUPSEARCH")]
        [CommandMethod("CADLOOKUPSEARCHDB")]
        public void SearchDrawingForCadLookup()
        {
            if (_viewModel == null || _hostAdapter == null || _requestedSearchQuery == null) return;
            var query = _requestedSearchQuery;
            _requestedSearchQuery = null;
            try
            {
                var results = _hostAdapter.SearchObjects(new SearchRequest(query));
                _viewModel.ShowDatabaseSearchResults(results);
            }
            catch (System.Exception ex)
            {
                VinaApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n" + ex.Message);
                _viewModel.StatusText = ex.Message;
            }
        }

        [CommandMethod("CADLOOKUPFIND")]
        public void OpenSearchDialogForCadLookup()
        {
            EnsureRibbon();
            EnsureViewModel();
            ShowInspectorWindow();
            _viewModel?.RequestOpenSearchDialog();
        }

        [CommandMethod("CADLOOKUPHLIGHT")]
        public void ToggleTransientHighlightForCadLookup()
        {
            if (_hostAdapter == null) return;
            var reference = _requestedHighlightReference;
            _requestedHighlightReference = null;
            try
            {
                _hostAdapter.SetTransientHighlight(reference);
                var localization = new LocalizationService();
                var message = reference == null ? localization["HighlightOff"] : localization["HighlightOn"];
                VinaApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n" + message);
                if (_viewModel != null) _viewModel.StatusText = message;
            }
            catch (System.Exception ex)
            {
                VinaApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n" + ex.Message);
            }
        }

        [CommandMethod("CADLOOKUPLANG")]
        [CommandMethod("CADLOOKUPLANGUAGE")]
        public void ChangeCadLookupLanguage()
        {
            var document = VinaApp.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            var localization = new LocalizationService();
            var next = localization.Language == "vi" ? "EN" : "VI";
            var result = document.Editor.GetString(string.Format(localization["LanguagePrompt"], next));
            if (result.Status != PromptStatus.OK && result.Status != PromptStatus.None) return;
            var input = result.StringResult?.Trim();
            var language = string.IsNullOrEmpty(input) ? (next == "EN" ? "en" : "vi") : ParseLanguage(input);
            if (language == null)
            {
                document.Editor.WriteMessage("\n" + localization["LanguageInvalid"]);
                return;
            }
            if (_viewModel != null) _viewModel.SetLanguage(language);
            else localization.Language = language;
            UpdateRibbonLanguage();
        }

        private static string? ParseLanguage(string input)
        {
            switch (input.Trim().ToUpperInvariant())
            {
                case "VI": case "VIE": case "VIETNAMESE": case "TIẾNG VIỆT": case "1": return "vi";
                case "EN": case "ENG": case "ENGLISH": case "TIẾNG ANH": case "2": return "en";
                default: return null;
            }
        }

        private static void EnsureViewModel()
        {
            if (_viewModel != null) return;
            _hostAdapter = new VinaCadHostAdapter();
            _viewModel = new MainViewModel(_hostAdapter, new ObjectInspector(_hostAdapter), new LocalizationService(),
                RequestSelection, RequestInspection, RequestHandleLookup, RequestSnoopDatabase,
                RequestZoomToObject, RequestSelectInDrawing, RequestDatabaseSearch, RequestTransientHighlight);
            _viewModel.LanguageChanged += _ => UpdateRibbonLanguage();
        }

        private static void ShowInspectorWindow()
        {
            if (_viewModel == null) return;
            if (_window == null)
            {
                _window = new InspectorWindow(_viewModel);
                _window.Closed += (_, __) => _window = null;
            }
            if (!_window.IsVisible) VinaApp.ShowModelessWindow(_window);
            if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
            _window.Activate();
        }

        private static void RequestSelection() => QueueCommand("CADLOOKUPSELECT");
        private static void RequestSnoopDatabase() => QueueCommand("CADLOOKUPSNOOPDB");
        private static void RequestInspection(CadObjectRef reference)
        {
            _requestedReference = reference;
            _requestedHandle = null;
            QueueCommand("CADLOOKUPINSPECT");
        }
        private static void RequestHandleLookup(string handle)
        {
            _requestedHandle = handle;
            _requestedReference = null;
            QueueCommand("CADLOOKUPINSPECT");
        }
        private static void RequestZoomToObject(CadObjectRef reference)
        {
            _requestedZoomReference = reference;
            QueueCommand("CADLOOKUPZOOM");
        }
        private static void RequestSelectInDrawing(CadObjectRef reference)
        {
            _requestedMarkReference = reference;
            QueueCommand("CADLOOKUPMARK");
        }
        private static void RequestDatabaseSearch(string query)
        {
            _requestedSearchQuery = query;
            QueueCommand("CADLOOKUPSEARCH");
        }
        private static void RequestTransientHighlight(CadObjectRef? reference)
        {
            _requestedHighlightReference = reference;
            QueueCommand("CADLOOKUPHLIGHT");
        }
        private static void QueueCommand(string command) =>
            VinaApp.DocumentManager.MdiActiveDocument?.SendStringToExecute(command + " ", true, false, false);

        private enum RibbonIcon { Inspect, Select, Database, Refresh, Language, Search }

        private sealed class RibbonCommand : ICommand
        {
            private readonly string _command;
            public RibbonCommand(string command) { _command = command; }
            public event EventHandler? CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object? parameter) => VinaApp.DocumentManager.MdiActiveDocument != null;
            public void Execute(object? parameter) => QueueCommand(_command);
        }
    }
}
