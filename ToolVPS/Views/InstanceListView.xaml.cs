using System.Windows.Controls;
using ToolVPS.ViewModels;

namespace ToolVPS.Views;

public partial class InstanceListView : UserControl
{
    public InstanceListView()
    {
        InitializeComponent();
    }

    private void QuickPasswordBox_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is InstanceListViewModel vm)
            vm.QuickPassword = QuickPasswordBox.Password;
    }
}
