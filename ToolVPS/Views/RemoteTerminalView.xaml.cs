using System.Windows.Controls;
using System.Windows.Input;
using ToolVPS.ViewModels;

namespace ToolVPS.Views;

public partial class RemoteTerminalView : UserControl
{
    public RemoteTerminalView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is TerminalViewModel vm)
                vm.TerminalOutputReceived += _ => TerminalScroll.ScrollToEnd();
        };
    }

    private void CmdInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is TerminalViewModel vm)
        {
            vm.SendCommandCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void FileList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is TerminalViewModel vm)
            vm.OpenFileOrDirectoryCommand.Execute(null);
    }
}
