using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Windows;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using AcadAppMgd = Autodesk.AutoCAD.ApplicationServices.Application;

namespace CADLookup.Host.AutoCAD
{
    public sealed class CadLookupCommands : IExtensionApplication
    {
        private static InspectorWindow? _window;
        private static MainViewModel? _viewModel;
        private static string? _requestedHandle;
        private static CadObjectRef? _requestedReference;
        private static Database? _watchedDatabase;
        private static ContextMenuExtension? _defaultContextMenu;
        private static ContextMenuExtension? _entityContextMenu;

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
            WatchDatabase(null);
            _window?.Close();
            _window = null;
            _viewModel = null;
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
                // 1. Menu khi click chuot phai tren vung ve khong chon doi tuong (Default Context Menu)
                _defaultContextMenu = new ContextMenuExtension { Title = "CADLookup" };
                var miInspect = new MenuItem("CADLookup - Tra cứu đối tượng...");
                miInspect.Click += (s, e) =>
                {
                    AcadApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("CADLOOKUP ", true, false, false);
                };
                _defaultContextMenu.MenuItems.Add(miInspect);

                var miSnoop = new MenuItem("CADLookup - Duyệt Database (Snoop DB)...");
                miSnoop.Click += (s, e) =>
                {
                    AcadApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("CADLOOKUPSNOOPDB ", true, false, false);
                };
                _defaultContextMenu.MenuItems.Add(miSnoop);

                AcadAppMgd.AddDefaultContextMenuExtension(_defaultContextMenu);

                // 2. Menu khi click chuot phai tren doi tuong dang chon (Object Context Menu)
                _entityContextMenu = new ContextMenuExtension { Title = "CADLookup" };
                var miEntityInspect = new MenuItem("CADLookup - Tra cứu đối tượng đã chọn");
                miEntityInspect.Click += (s, e) =>
                {
                    AcadApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("CADLOOKUP ", true, false, false);
                };
                _entityContextMenu.MenuItems.Add(miEntityInspect);

                var miEntitySnoop = new MenuItem("CADLookup - Duyệt Database (Snoop DB)...");
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
            if (tab.Panels.Any(panel => panel.Source?.Id == panelId)) return;
            var source = new RibbonPanelSource { Id = panelId, Title = "Object Inspector" };
            var panel = new RibbonPanel { Source = source };
            tab.Panels.Add(panel);
            // Mỗi nút một màu riêng de nhan biet nhanh tren Ribbon
            source.Items.Add(new RibbonButton
            {
                Id = "CADLOOKUP_OPEN_BUTTON",
                Text = "Inspect",
                ShowText = true,
                ShowImage = true,
                Size = RibbonItemSize.Large,
                Orientation = WpfOrientation.Vertical,
                ToolTip = "Open CADLookup to inspect selected drawing objects",
                LargeImage = CreateIcon(RibbonIcon.Inspect, Color.FromRgb(37, 99, 235)),
                Image = CreateIcon(RibbonIcon.Inspect, Color.FromRgb(37, 99, 235)),
                CommandHandler = new RibbonCommand("CADLOOKUP ")
            });
            source.Items.Add(new RibbonButton
            {
                Id = "CADLOOKUP_SELECT_BUTTON",
                Text = "Select",
                ShowText = true,
                ShowImage = true,
                Size = RibbonItemSize.Large,
                Orientation = WpfOrientation.Vertical,
                ToolTip = "Choose drawing objects for CADLookup",
                LargeImage = CreateIcon(RibbonIcon.Select, Color.FromRgb(16, 185, 129)),
                Image = CreateIcon(RibbonIcon.Select, Color.FromRgb(16, 185, 129)),
                CommandHandler = new RibbonCommand("CADLOOKUPSELECT ")
            });
            source.Items.Add(new RibbonButton
            {
                Id = "CADLOOKUP_SNOOP_BUTTON",
                Text = "Snoop DB",
                ShowText = true,
                ShowImage = true,
                Size = RibbonItemSize.Large,
                Orientation = WpfOrientation.Vertical,
                ToolTip = "Snoop AutoCAD database tables and dictionaries",
                LargeImage = CreateIcon(RibbonIcon.SnoopDatabase, Color.FromRgb(139, 92, 246)),
                Image = CreateIcon(RibbonIcon.SnoopDatabase, Color.FromRgb(139, 92, 246)),
                CommandHandler = new RibbonCommand("CADLOOKUPSNOOPDB ")
            });
            source.Items.Add(new RibbonButton
            {
                Id = "CADLOOKUP_REFRESH_BUTTON",
                Text = "Refresh",
                ShowText = true,
                ShowImage = true,
                Size = RibbonItemSize.Large,
                Orientation = WpfOrientation.Vertical,
                ToolTip = "Reload the current CADLookup inspection",
                LargeImage = CreateIcon(RibbonIcon.Refresh, Color.FromRgb(245, 158, 11)),
                Image = CreateIcon(RibbonIcon.Refresh, Color.FromRgb(245, 158, 11)),
                CommandHandler = new RibbonCommand("CADLOOKUPREFRESH ")
            });
        }

        private static ImageSource CreateIcon(RibbonIcon icon, Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            var pen = new Pen(brush, 1.6);
            pen.Freeze();
            var drawing = new GeometryDrawing(null, pen, Geometry.Parse(IconGeometry(icon)));
            drawing.Freeze();
            var image = new DrawingImage(drawing);
            image.Freeze();
            return image;
        }

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
                default:
                    return "M4,4 L16,4 L16,16 L4,16 Z";
            }
        }

        private enum RibbonIcon { Inspect, Select, SnoopDatabase, Refresh }

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
            var host = new AutoCadHostAdapter();
            if (_viewModel == null)
                _viewModel = new MainViewModel(host, new ObjectInspector(host), new LocalizationService(), RequestSelection, RequestInspection, RequestHandleLookup, RequestSnoopDatabase);

            // Kiem tra nguoi dung co chon doi tuong truoc khi go lenh hay khong
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            bool hasPreselection = false;
            if (doc != null)
            {
                var implied = doc.Editor.SelectImplied();
                if (implied != null && implied.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK && implied.Value != null && implied.Value.Count > 0)
                {
                    hasPreselection = true;
                }
            }

            if (hasPreselection)
            {
                // Nguoi dung chon doi tuong truoc -> mo bang voi doi tuong do luon
                _viewModel.UpdateSelectionFromHost(false);
            }
            else
            {
                // Nguoi dung go lenh truoc -> hoi chon tren man hinh (Enter de bo qua van mo bang)
                _viewModel.UpdateSelectionFromHost(true);
            }

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

        [CommandMethod("CADLOOKUPSELECT")]
        public void SelectForCadLookup()
        {
            if (_viewModel == null) ShowCadLookup();
            _viewModel?.UpdateSelectionFromHost(true);
        }

        [CommandMethod("CADLOOKUPSNOOPDB")]
        [CommandMethod("CADLOOKUPDB")]
        public void SnoopDatabaseForCadLookup()
        {
            EnsureRibbon();
            EnsureContextMenu();
            var host = new AutoCadHostAdapter();
            if (_viewModel == null)
                _viewModel = new MainViewModel(host, new ObjectInspector(host), new LocalizationService(), RequestSelection, RequestInspection, RequestHandleLookup, RequestSnoopDatabase);
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
            _viewModel?.SnoopDatabaseFromHost();
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

        [CommandMethod("CADLOOKUPREFRESH")]
        public void RefreshForCadLookup()
        {
            if (_viewModel == null) { ShowCadLookup(); return; }
            var reference = _viewModel?.CurrentReference;
            var document = AcadApp.DocumentManager.MdiActiveDocument;
            if (reference != null && document != null &&
                string.Equals(reference.DocumentId, document.Name, System.StringComparison.OrdinalIgnoreCase))
                _viewModel?.SelectObject(reference, false);
            else _viewModel?.UpdateSelectionFromHost(false);
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
