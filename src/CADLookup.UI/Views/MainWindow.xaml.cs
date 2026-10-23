using System.Windows.Controls;
using System.Windows.Input;
using CADLookup.Core.Contracts;
using CADLookup.UI.ViewModels;

namespace CADLookup.UI.Views
{
    public partial class MainView : UserControl
    {
        public MainView(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
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
