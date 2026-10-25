using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using CADLookup.Core.Application;
using CADLookup.Core.Contracts;
using CADLookup.UI.Localization;
using CADLookup.UI.ViewModels;
using CADLookup.UI.Views;
using System.Windows.Interop;
using System.Windows.Input;
using Autodesk.Windows;
using WpfOrientation = System.Windows.Controls.Orientation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DrawingIcon = System.Drawing.Icon;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using AcadAppMgd = Autodesk.AutoCAD.ApplicationServices.Application;

namespace CADLookup.Host.AutoCAD
{
    public sealed class CadLookupCommands : IExtensionApplication
    {
        private static InspectorWindow? _window;
        private static MainViewModel? _viewModel;
        private static AutoCadHostAdapter? _hostAdapter;
        private static string? _requestedHandle;
        private static CadObjectRef? _requestedReference;
        private static CadObjectRef? _requestedZoomReference;
        private static CadObjectRef? _requestedMarkReference;
        private static string? _requestedSearchQuery;
        private static CadObjectRef? _requestedHighlightReference;
        private static Database? _watchedDatabase;
        private static ContextMenuExtension? _defaultContextMenu;
        private static ContextMenuExtension? _entityContextMenu;
        private static readonly List<DrawingIcon> ContextMenuIcons = new List<DrawingIcon>();
        private static readonly List<IntPtr> ContextMenuIconHandles = new List<IntPtr>();

        public void Initialize()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveBundleAssembly;
            AcadApp.Idle += OnApplicationIdle;
            AcadApp.DocumentManager.DocumentActivated += OnDocumentActivated;
            AcadApp.DocumentManager.DocumentToBeDestroyed += OnDocumentToBeDestroyed;
            WatchDatabase(AcadApp.DocumentManager.MdiActiveDocument?.Database);
            EnsureContextMenu();
        }

        public void Terminate()
        {
            AppDomain.CurrentDomain.AssemblyResolve -= ResolveBundleAssembly;
            AcadApp.Idle -= OnApplicationIdle;
            AcadApp.DocumentManager.DocumentActivated -= OnDocumentActivated;
            AcadApp.DocumentManager.DocumentToBeDestroyed -= OnDocumentToBeDestroyed;
            RemoveContextMenu();
            AcadApp.Idle -= OnLocalizationUiRefreshIdle;
            if (_viewModel != null) _viewModel.LanguageChanged -= OnViewModelLanguageChanged;
            WatchDatabase(null);
            _hostAdapter?.SetTransientHighlight(null);
            _window?.Close();
            _window = null;
            _viewModel = null;
            _hostAdapter = null;
        }

        private static Assembly? ResolveBundleAssembly(object? sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name).Name + ".dll";
            var pluginDirectory = Path.GetDirectoryName(typeof(CadLookupCommands).Assembly.Location);
            if (string.IsNullOrEmpty(pluginDirectory)) return null;
            var candidate = Path.Combine(pluginDirectory, assemblyName);
            return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
        }

        private static void OnApplicationIdle(object? sender, EventArgs e)
        {
            EnsureRibbon();
            EnsureContextMenu();
        }

        private static void EnsureContextMenu()
        {
            if (_defaultContextMenu != null) return;
            try
            {
                var localization = new LocalizationService();
                // 1. Menu khi click chuot phai tren vung ve khong chon doi tuong (Default Context Menu)
                _defaultContextMenu = new ContextMenuExtension { Title = "CADLookup" };
                var miInspect = new MenuItem(localization["ContextInspect"]);
                miInspect.Icon = CreateMenuIcon(RibbonIcon.Inspect, Color.FromRgb(37, 99, 235));
                miInspect.Click += (s, e) =>
                {
                    AcadApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("CADLOOKUP ", true, false, false);
                };
                _defaultContextMenu.MenuItems.Add(miInspect);

                var miSnoop = new MenuItem(localization["ContextDatabase"]);
                miSnoop.Icon = CreateMenuIcon(RibbonIcon.SnoopDatabase, Color.FromRgb(139, 92, 246));
                miSnoop.Click += (s, e) =>
                {
                    AcadApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("CADLOOKUPSNOOPDB ", true, false, false);
                };
                _defaultContextMenu.MenuItems.Add(miSnoop);

                var miSearch = new MenuItem(localization["ContextSearch"]);
                miSearch.Icon = CreateMenuIcon(RibbonIcon.Search, Color.FromRgb(245, 158, 11));
                miSearch.Click += (s, e) =>
                {
                    AcadApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("CADLOOKUPFIND ", true, false, false);
                };
                _defaultContextMenu.MenuItems.Add(miSearch);

                AcadAppMgd.AddDefaultContextMenuExtension(_defaultContextMenu);

                // 2. Menu khi click chuot phai tren doi tuong dang chon (Object Context Menu)
                _entityContextMenu = new ContextMenuExtension { Title = "CADLookup" };
                var miEntityInspect = new MenuItem(localization["ContextInspectSelected"]);
                miEntityInspect.Icon = CreateMenuIcon(RibbonIcon.Inspect, Color.FromRgb(37, 99, 235));
                miEntityInspect.Click += (s, e) =>
                {
                    AcadApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("CADLOOKUP ", true, false, false);
                };
                _entityContextMenu.MenuItems.Add(miEntityInspect);

                var miEntitySnoop = new MenuItem(localization["ContextDatabase"]);
                miEntitySnoop.Icon = CreateMenuIcon(RibbonIcon.SnoopDatabase, Color.FromRgb(139, 92, 246));
                miEntitySnoop.Click += (s, e) =>
                {
                    AcadApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("CADLOOKUPSNOOPDB ", true, false, false);
                };
                _entityContextMenu.MenuItems.Add(miEntitySnoop);

                AcadAppMgd.AddObjectContextMenuExtension(RXObject.GetClass(typeof(Entity)), _entityContextMenu);
            }
            catch
            {
                // Suppress if already registered
            }
        }

        private static void RemoveContextMenu()
        {
            try
            {
                if (_defaultContextMenu != null)
                {
                    AcadAppMgd.RemoveDefaultContextMenuExtension(_defaultContextMenu);
                    _defaultContextMenu = null;
                }
                if (_entityContextMenu != null)
                {
                    AcadAppMgd.RemoveObjectContextMenuExtension(RXObject.GetClass(typeof(Entity)), _entityContextMenu);
                    _entityContextMenu = null;
                }
            }
            catch { }
            DisposeContextMenuIcons();
        }

        private static void EnsureRibbon()
        {
            var ribbon = ComponentManager.Ribbon;
            if (ribbon == null || ribbon.Tabs == null) return;
            AcadApp.Idle -= OnApplicationIdle;

            const string tabId = "CADLOOKUP_TAB";
            var tab = ribbon.Tabs.FirstOrDefault(item => item.Id == tabId);
            if (tab == null)
            {
                tab = new RibbonTab { Id = tabId, Title = "CADLookup" };
                ribbon.Tabs.Add(tab);
            }

            const string panelId = "CADLOOKUP_PANEL";
            var panel = tab.Panels.FirstOrDefault(item => item.Source?.Id == panelId);
            if (panel == null)
            {
                var source = new RibbonPanelSource { Id = panelId, Title = "Object Inspector" };
                panel = new RibbonPanel { Source = source };
                tab.Panels.Add(panel);
            }

            var panelSource = panel.Source;
            if (panelSource == null) return;

            // Keep the panel safe to re-enter after AutoCAD reloads the plug-in.
            GetOrCreateRibbonButton(panelSource, "CADLOOKUP_OPEN_BUTTON", RibbonIcon.Inspect,
                Color.FromRgb(37, 99, 235), "CADLOOKUP ");
            GetOrCreateRibbonButton(panelSource, "CADLOOKUP_SELECT_BUTTON", RibbonIcon.Select,
                Color.FromRgb(16, 185, 129), "CADLOOKUPSELECT ");
            GetOrCreateRibbonButton(panelSource, "CADLOOKUP_SNOOP_BUTTON", RibbonIcon.SnoopDatabase,
                Color.FromRgb(139, 92, 246), "CADLOOKUPSNOOPDB ");
            GetOrCreateRibbonButton(panelSource, "CADLOOKUP_SEARCH_BUTTON", RibbonIcon.Search,
                Color.FromRgb(245, 158, 11), "CADLOOKUPFIND ");
            GetOrCreateRibbonButton(panelSource, "CADLOOKUP_REFRESH_BUTTON", RibbonIcon.Refresh,
                Color.FromRgb(245, 158, 11), "CADLOOKUPREFRESH ");
            GetOrCreateRibbonButton(panelSource, "CADLOOKUP_LANGUAGE_BUTTON", RibbonIcon.Language,
                Color.FromRgb(14, 165, 233), "CADLOOKUPLANG ");
            UpdateRibbonLanguage();
        }

        private static RibbonButton GetOrCreateRibbonButton(RibbonPanelSource source, string id,
            RibbonIcon icon, Color color, string command)
        {
            var button = source.Items.OfType<RibbonButton>().FirstOrDefault(item => item.Id == id);
            if (button == null)
            {
                button = new RibbonButton { Id = id, CommandHandler = new RibbonCommand(command) };
                source.Items.Add(button);
            }

            // Use AutoCAD's expected 32px large glyph and a separate 16px compact glyph.
            button.ShowText = true;
            button.ShowImage = true;
            button.Size = RibbonItemSize.Large;
            button.Orientation = WpfOrientation.Vertical;
            button.LargeImage = CreateIcon(icon, color, 32);
            button.Image = CreateIcon(icon, color, 16);
            return button;
        }

        private static void UpdateRibbonLanguage()
        {
            var ribbon = ComponentManager.Ribbon;
            var tab = ribbon?.Tabs?.FirstOrDefault(item => item.Id == "CADLOOKUP_TAB");
            if (tab == null) return;
            var source = tab.Panels.FirstOrDefault(item => item.Source?.Id == "CADLOOKUP_PANEL")?.Source;
            if (source == null) return;

            var localization = new LocalizationService();
            tab.Title = "CADLookup";
            source.Title = localization["RibbonPanelTitle"];
            SetRibbonButtonText(source, "CADLOOKUP_OPEN_BUTTON", localization["RibbonInspect"], localization["RibbonInspectHint"]);
            SetRibbonButtonText(source, "CADLOOKUP_SELECT_BUTTON", localization["RibbonSelect"], localization["RibbonSelectHint"]);
            SetRibbonButtonText(source, "CADLOOKUP_SNOOP_BUTTON", localization["RibbonSnoop"], localization["RibbonSnoopHint"]);
            SetRibbonButtonText(source, "CADLOOKUP_SEARCH_BUTTON", localization["RibbonSearch"], localization["RibbonSearchHint"]);
            SetRibbonButtonText(source, "CADLOOKUP_REFRESH_BUTTON", localization["RibbonRefresh"], localization["RibbonRefreshHint"]);
            SetRibbonButtonText(source, "CADLOOKUP_LANGUAGE_BUTTON", localization["Language"], localization["RibbonLanguageHint"]);
        }

        private static void SetRibbonButtonText(RibbonPanelSource source, string id, string text, string toolTip)
        {
            var button = source.Items.OfType<RibbonButton>().FirstOrDefault(item => item.Id == id);
            if (button == null) return;
            button.Text = text;
            button.ToolTip = toolTip;
        }

        private static ImageSource CreateIcon(RibbonIcon icon, Color color, int size)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            var scale = size * (1.5 / 32.0);
            var offset = size * (1.0 / 32.0);
            // Geometry.Parse returns a frozen Freezable; transform a new mutable group around it.
            var geometry = new GeometryGroup();
            geometry.Children.Add(Geometry.Parse(IconGeometry(icon)));
            geometry.Transform = new MatrixTransform(scale, 0, 0, scale, offset, offset);
            var pen = new Pen(brush, size * (2.2 / 32.0))
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            pen.Freeze();
            var group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing(Brushes.Transparent, null,
                new RectangleGeometry(new System.Windows.Rect(0, 0, size, size))));
            group.Children.Add(new GeometryDrawing(null, pen, geometry));
            group.Freeze();
            var image = new DrawingImage(group);
            image.Freeze();
            return image;
        }

        private static DrawingIcon CreateMenuIcon(RibbonIcon icon, Color color)
        {
            const int size = 16;
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                context.DrawImage(CreateIcon(icon, color, size), new System.Windows.Rect(0, 0, size, size));
            }

            var bitmapSource = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmapSource.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            using (var png = new MemoryStream())
            {
                encoder.Save(png);
                png.Position = 0;
                using (var decoded = new System.Drawing.Bitmap(png))
                using (var bitmap = new System.Drawing.Bitmap(decoded))
                {
                    var handle = bitmap.GetHicon();
                    try
                    {
                        var menuIcon = DrawingIcon.FromHandle(handle);
                        ContextMenuIcons.Add(menuIcon);
                        ContextMenuIconHandles.Add(handle);
                        return menuIcon;
                    }
                    catch
                    {
                        DestroyIcon(handle);
                        throw;
                    }
                }
            }
        }

        private static void DisposeContextMenuIcons()
        {
            foreach (var icon in ContextMenuIcons) icon.Dispose();
            ContextMenuIcons.Clear();
            foreach (var handle in ContextMenuIconHandles) DestroyIcon(handle);
            ContextMenuIconHandles.Clear();
        }

        [DllImport("user32.dll", EntryPoint = "DestroyIcon", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr handle);

        private static string IconGeometry(RibbonIcon icon)
        {
            switch (icon)
            {
                // Phong kinh soi document: trang giay + kinh lup
                case RibbonIcon.Inspect:
                    return "M4,2 L12,2 L12,8 M4,2 L4,16 L10,16 M6,5 L10,5 M6,8 L9,8 " +
                        "M15.2,10.8 A3.4,3.4 0 1,1 10.8,15.2 A3.4,3.4 0 1,1 15.2,10.8 M17.8,17.8 L14.6,14.6";
                // Con tro chon doi tuong, co bong dap
                case RibbonIcon.Select:
                    return "M5,3 L16.5,10 L11.4,11.2 L9.2,16.4 Z M11,12 L15.5,16.5";
                // Cylinder database 3 lop
                case RibbonIcon.SnoopDatabase:
                    return "M4,5 A6,2.4 0 1,0 16,5 A6,2.4 0 1,0 4,5 M4,5 L4,14.5 A6,2.4 0 1,0 16,14.5 L16,5 " +
                        "M4,9.75 A6,2.4 0 1,0 16,9.75";
                // Mui ten tron refresh
                case RibbonIcon.Refresh:
                    return "M15.6,6.4 A6,6 0 1,0 16,10 M15.6,6.4 L15.6,2.4 M15.6,6.4 L11.6,6.4";
                // Mui ten tim kiem (kinh lup)
                case RibbonIcon.Search:
                    return "M3.5,8.2 A4.7,4.7 0 1,1 12.9,8.2 A4.7,4.7 0 1,1 3.5,8.2 M12.2,11.9 L16.8,16.5";
                case RibbonIcon.Language:
                    return "M10,2 A8,8 0 1,0 10,18 A8,8 0 1,0 10,2 M2,10 L18,10 M10,2 C6,6 6,14 10,18 M10,2 C14,6 14,14 10,18";
                default:
                    return "M4,4 L16,4 L16,16 L4,16 Z";
            }
        }

        private enum RibbonIcon { Inspect, Select, SnoopDatabase, Refresh, Language, Search }

        private sealed class RibbonCommand : ICommand
        {
            private readonly string _command;
            public RibbonCommand(string command) { _command = command; }
            public event EventHandler? CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object parameter) => AcadApp.DocumentManager.MdiActiveDocument != null;
            public void Execute(object parameter) =>
                AcadApp.DocumentManager.MdiActiveDocument?.SendStringToExecute(_command, true, false, false);
        }

        [CommandMethod("CADLOOKUP", CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void ShowCadLookup()
        {
            EnsureRibbon();
            EnsureContextMenu();
            EnsureViewModel();

            // Tra cứu only consumes a pickfirst selection; without one it opens the empty inspector.
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                var implied = doc.Editor.SelectImplied();
                if (implied != null && implied.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK && implied.Value != null && implied.Value.Count > 0)
                {
                    _viewModel!.UpdateSelectionFromHost(false);
                }
            }
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
            EnsureContextMenu();
            EnsureViewModel();
            ShowInspectorWindow();
            _viewModel?.SnoopDatabaseFromHost();
        }

        private static void EnsureViewModel()
        {
            if (_viewModel != null) return;
            _hostAdapter = new AutoCadHostAdapter();
            _viewModel = new MainViewModel(_hostAdapter, new ObjectInspector(_hostAdapter), new LocalizationService(),
                RequestSelection, RequestInspection, RequestHandleLookup, RequestSnoopDatabase,
                RequestZoomToObject, RequestSelectInDrawing, RequestDatabaseSearch, RequestTransientHighlight);
            _viewModel.LanguageChanged += OnViewModelLanguageChanged;
        }

        private static void ShowInspectorWindow()
        {
            if (_viewModel == null) return;
            if (_window == null)
            {
                _window = new InspectorWindow(_viewModel);
                var helper = new WindowInteropHelper(_window);
                helper.Owner = AcadApp.MainWindow.Handle;
                _window.Closed += (_, __) => _window = null;
            }
            if (!_window.IsVisible) _window.Show();
            if (_window.WindowState == System.Windows.WindowState.Minimized)
                _window.WindowState = System.Windows.WindowState.Normal;
            _window.Activate();
        }

        [CommandMethod("CADLOOKUPINSPECT")]
        public void InspectForCadLookup()
        {
            if (_requestedReference != null)
            {
                _viewModel?.SelectObject(_requestedReference);
                _requestedReference = null;
            }
            else if (_requestedHandle != null) _viewModel?.InspectFromHost(_requestedHandle);
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
                AcadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n" + localization["ZoomedToObject"]);
                if (_viewModel != null) _viewModel.StatusText = localization["ZoomedToObject"];
            }
            catch (System.Exception ex)
            {
                AcadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n" + ex.Message);
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
                AcadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n" + localization["MarkedInDrawing"]);
                if (_viewModel != null) _viewModel.StatusText = localization["MarkedInDrawing"];
            }
            catch (System.Exception ex)
            {
                AcadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n" + ex.Message);
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
                AcadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n" + ex.Message);
                _viewModel.StatusText = ex.Message;
            }
        }

        [CommandMethod("CADLOOKUPFIND")]
        public void OpenSearchDialogForCadLookup()
        {
            EnsureRibbon();
            EnsureContextMenu();
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
                AcadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n" + message);
                if (_viewModel != null) _viewModel.StatusText = message;
            }
            catch (System.Exception ex)
            {
                AcadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n" + ex.Message);
            }
        }

        [CommandMethod("CADLOOKUPREFRESH")]
        public void RefreshForCadLookup()
        {
            // Sent on document activation; it must not open the inspector as a startup side effect.
            if (_viewModel == null) return;
            var reference = _viewModel?.CurrentReference;
            var document = AcadApp.DocumentManager.MdiActiveDocument;
            if (reference != null && document != null &&
                string.Equals(reference.DocumentId, document.Name, System.StringComparison.OrdinalIgnoreCase))
                _viewModel?.SelectObject(reference, false);
            else _viewModel?.UpdateSelectionFromHost(false);
        }

        [CommandMethod("CADLOOKUPLANG")]
        [CommandMethod("CADLOOKUPLANGUAGE")]
        public void ChangeCadLookupLanguage()
        {
            var document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null) return;

            var localization = new LocalizationService();
            var defaultLanguage = localization.Language == "vi" ? "EN" : "VI";
            var prompt = string.Format(localization["LanguagePrompt"], defaultLanguage);
            var result = document.Editor.GetString(prompt);
            if (result.Status != PromptStatus.OK && result.Status != PromptStatus.None) return;

            var input = result.StringResult?.Trim() ?? string.Empty;
            string targetLanguage;
            if (string.IsNullOrEmpty(input))
            {
                targetLanguage = defaultLanguage == "EN" ? "en" : "vi";
            }
            else if (!TryParseLanguage(input, out targetLanguage))
            {
                document.Editor.WriteMessage("\n" + localization["LanguageInvalid"]);
                return;
            }

            var previousLanguage = localization.Language;
            if (_viewModel != null)
                _viewModel.SetLanguage(targetLanguage);
            else
                localization.Language = targetLanguage;

            ScheduleLocalizationUiRefresh();

            var updatedLocalization = new LocalizationService();
            var languageName = updatedLocalization[targetLanguage == "vi" ? "Vietnamese" : "English"];
            var messageKey = previousLanguage == targetLanguage ? "LanguageAlready" : "LanguageChanged";
            document.Editor.WriteMessage("\n" + string.Format(updatedLocalization[messageKey], languageName));
        }

        private static bool TryParseLanguage(string input, out string language)
        {
            switch (input.Trim().ToUpperInvariant())
            {
                case "VI":
                case "VIE":
                case "VIETNAMESE":
                case "TIẾNG VIỆT":
                case "1":
                    language = "vi";
                    return true;
                case "EN":
                case "ENG":
                case "ENGLISH":
                case "TIẾNG ANH":
                case "2":
                    language = "en";
                    return true;
                default:
                    language = string.Empty;
                    return false;
            }
        }

        private static void OnViewModelLanguageChanged(string language) => ScheduleLocalizationUiRefresh();

        private static void ScheduleLocalizationUiRefresh()
        {
            AcadApp.Idle -= OnLocalizationUiRefreshIdle;
            AcadApp.Idle += OnLocalizationUiRefreshIdle;
        }

        private static void OnLocalizationUiRefreshIdle(object? sender, EventArgs e)
        {
            AcadApp.Idle -= OnLocalizationUiRefreshIdle;
            UpdateRibbonLanguage();
            RemoveContextMenu();
            EnsureContextMenu();
        }

        private static void RequestSelection()
        {
            AcadApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("CADLOOKUPSELECT ", true, false, false);
        }

        private static void RequestSnoopDatabase()
        {
            AcadApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("CADLOOKUPSNOOPDB ", true, false, false);
        }

        private static void RequestInspection(CadObjectRef reference)
        {
            _requestedReference = reference;
            AcadApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("CADLOOKUPINSPECT ", true, false, false);
        }

        private static void RequestHandleLookup(string handle)
        {
            _requestedHandle = handle;
            _requestedReference = null;
            AcadApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("CADLOOKUPINSPECT ", true, false, false);
        }

        private static void RequestZoomToObject(CadObjectRef reference)
        {
            _requestedZoomReference = reference;
            AcadApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("CADLOOKUPZOOM ", true, false, false);
        }

        private static void RequestSelectInDrawing(CadObjectRef reference)
        {
            _requestedMarkReference = reference;
            AcadApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("CADLOOKUPMARK ", true, false, false);
        }

        private static void RequestDatabaseSearch(string query)
        {
            _requestedSearchQuery = query;
            AcadApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("CADLOOKUPSEARCH ", true, false, false);
        }

        private static void RequestTransientHighlight(CadObjectRef? reference)
        {
            _requestedHighlightReference = reference;
            AcadApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("CADLOOKUPHLIGHT ", true, false, false);
        }

        private static void OnDocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            WatchDatabase(e.Document?.Database);
            e.Document?.SendStringToExecute("CADLOOKUPREFRESH ", true, false, false);
        }

        private static void OnDocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document == null) return;
            _viewModel?.ClearForDocumentClose(e.Document.Name);
            if (e.Document.Database == _watchedDatabase) WatchDatabase(null);
        }

        private static void OnObjectErased(object sender, ObjectErasedEventArgs e)
        {
            var current = _viewModel?.CurrentReference;
            if (current == null || e.DBObject == null ||
                !string.Equals(current.Handle, e.DBObject.Handle.ToString(), System.StringComparison.OrdinalIgnoreCase)) return;
            AcadApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("CADLOOKUPREFRESH ", true, false, false);
        }

        private static void WatchDatabase(Database? database)
        {
            if (_watchedDatabase == database) return;
            if (_watchedDatabase != null) _watchedDatabase.ObjectErased -= OnObjectErased;
            _watchedDatabase = database;
            if (_watchedDatabase != null) _watchedDatabase.ObjectErased += OnObjectErased;
        }
    }
}
