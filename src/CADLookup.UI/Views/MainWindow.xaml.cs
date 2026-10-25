using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CADLookup.Core.Contracts;
using CADLookup.UI.Themes;
using CADLookup.UI.ViewModels;

namespace CADLookup.UI.Views
{
    public partial class MainView : UserControl
    {
        public MainView(MainViewModel viewModel)
        {
            AppThemeManager.EnsureHandyControlLoaded();
            InitializeComponent();
            DataContext = viewModel;
            AppThemeManager.ApplyTheme(this, viewModel.Theme);
            viewModel.ThemeChanged += theme => AppThemeManager.ApplyTheme(this, theme);
        }

        private void ObjectTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is MainViewModel vm && e.NewValue is ObjectTreeNode node)
            {
                if (node.Reference != null)
                {
                    vm.RequestSelectObject(node.Reference);
                }
                else if (node.Children.Count > 0)
                {
                    var firstChild = node.Children.FirstOrDefault(c => c.Reference != null);
                    if (firstChild?.Reference != null)
                    {
                        vm.RequestSelectObject(firstChild.Reference);
                    }
                }
            }
        }

        private void ObjectSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm && sender is ListBox list && list.SelectedItem is CadObjectRef reference)
                vm.RequestSelectObject(reference);
        }

        private void PropertyDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm) vm.NavigateToSelectedProperty();
        }
    }
}
