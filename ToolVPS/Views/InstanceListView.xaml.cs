using System.Windows;
using System.Windows.Controls;
using ToolVPS.ViewModels;

namespace ToolVPS.Views;

public partial class InstanceListView : UserControl
{
    public InstanceListView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Populate the API key box with the saved value on load
        if (e.NewValue is InstanceListViewModel vm && !string.IsNullOrEmpty(vm.ApiKey))
            ApiKeyBox.Password = vm.ApiKey;
    }

    private void ApiKeyBox_Changed(object sender, RoutedEventArgs e)
    {
        if (DataContext is InstanceListViewModel vm)
            vm.ApiKey = ApiKeyBox.Password;
    }

    private void QuickPasswordBox_Changed(object sender, RoutedEventArgs e)
    {
        if (DataContext is InstanceListViewModel vm)
            vm.QuickPassword = QuickPasswordBox.Password;
    }
}
