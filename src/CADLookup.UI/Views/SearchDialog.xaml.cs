using System.Windows;
using System.Windows.Input;
using CADLookup.UI.Themes;
using CADLookup.UI.ViewModels;

namespace CADLookup.UI.Views
{
    public sealed partial class SearchDialog : Window
    {
        private readonly MainViewModel _viewModel;

        public SearchDialog(MainViewModel viewModel)
        {
            AppThemeManager.EnsureHandyControlLoaded();
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
            AppThemeManager.ApplyTheme(this, viewModel.Theme);
            _viewModel.ThemeChanged += OnThemeChanged;
            Closed += (_, __) => _viewModel.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged(string theme) => AppThemeManager.ApplyTheme(this, theme);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Enter && SearchResultsGrid.IsKeyboardFocusWithin)
            {
                OpenSelectedResult();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private void ResultDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OpenSelectedResult();
        }

        private void OpenSelectedResult()
        {
            if (SearchResultsGrid.SelectedItem is Core.Contracts.CadObjectRef reference)
                _viewModel.OpenSearchResult(reference);
        }
    }
}
