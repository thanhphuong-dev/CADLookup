using System.Windows;

namespace CADLookup.UI.Mvvm
{
    public sealed class BindingProxy : Freezable
    {
        public object? Data
        {
            get => GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
            nameof(Data), typeof(object), typeof(BindingProxy), new UIPropertyMetadata(null));

        protected override Freezable CreateInstanceCore() => new BindingProxy();
    }
}
