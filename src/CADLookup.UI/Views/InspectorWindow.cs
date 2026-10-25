using System.Windows;
using CADLookup.Infrastructure.Configuration;
using CADLookup.UI.Themes;
using CADLookup.UI.ViewModels;

namespace CADLookup.UI.Views
{
    public sealed partial class InspectorWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private SearchDialog? _searchDialog;

        public InspectorWindow(MainViewModel viewModel)
        {
            AppThemeManager.EnsureHandyControlLoaded();
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
            MainViewHost.Content = new MainView(viewModel);
            AppThemeManager.ApplyTheme(this, viewModel.Theme);
            _viewModel.ThemeChanged += OnThemeChanged;
            _viewModel.SearchDialogRequested += OpenSearchDialog;
            Closed += (_, __) =>
            {
                _viewModel.ThemeChanged -= OnThemeChanged;
                _viewModel.SearchDialogRequested -= OpenSearchDialog;
                _viewModel.ResetTransientHighlight();
                _searchDialog?.Close();
            };
            Closing += (_, __) => SaveWindowBounds();
            RestoreWindowBounds();
        }

        private void OnThemeChanged(string theme) => AppThemeManager.ApplyTheme(this, theme);

        private void OpenSearchDialog()
        {
            if (_searchDialog == null)
            {
                _searchDialog = new SearchDialog(_viewModel) { Owner = this };
                _searchDialog.Closed += (_, __) => _searchDialog = null;
            }
            if (!_searchDialog.IsVisible) _searchDialog.Show();
            if (_searchDialog.WindowState == WindowState.Minimized)
                _searchDialog.WindowState = WindowState.Normal;
            _searchDialog.Activate();
        }

        private void RestoreWindowBounds()
        {
            var bounds = UserSettingsStore.LoadWindowBounds();
            if (bounds == null) return;
            var virtualLeft = SystemParameters.VirtualScreenLeft;
            var virtualTop = SystemParameters.VirtualScreenTop;
            var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
            var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;
            // Ignore stored bounds that fall entirely outside the current desktop (monitor layout changed).
            if (bounds.Left + bounds.Width <= virtualLeft || bounds.Left >= virtualRight ||
                bounds.Top + bounds.Height <= virtualTop || bounds.Top >= virtualBottom) return;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = bounds.Left;
            Top = bounds.Top;
            if (bounds.Maximized)
            {
                WindowState = WindowState.Maximized;
                return;
            }
            Width = bounds.Width;
            Height = bounds.Height;
        }

        private void SaveWindowBounds()
        {
            var maximized = WindowState == WindowState.Maximized;
            var bounds = maximized ? RestoreBounds : new Rect(Left, Top, ActualWidth, ActualHeight);
            UserSettingsStore.SaveWindowBounds(bounds.Left, bounds.Top, bounds.Width, bounds.Height, maximized);
        }
    }
}
