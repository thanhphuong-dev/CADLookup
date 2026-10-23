using System.Windows;
using CADLookup.UI.ViewModels;

namespace CADLookup.UI.Views
{
    public sealed class InspectorWindow : Window
    {
        public InspectorWindow(MainViewModel viewModel)
        {
            Title = "CADLookup — Object Inspector";
            Width = 1280;
            Height = 820;
            MinWidth = 940;
            MinHeight = 620;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Content = new MainView(viewModel);
        }
    }
}
