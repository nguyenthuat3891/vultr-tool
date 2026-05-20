using System.Windows.Controls;
using ToolVPS.ViewModels;

namespace ToolVPS.Views;

public partial class SshKeyView : UserControl
{
    public SshKeyView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => BindPublicKeyBox();
    }

    private void BindPublicKeyBox()
    {
        if (DataContext is not SshKeyViewModel vm) return;

        void Refresh()
        {
            var key = vm.SelectedKey?.PublicKey ?? vm.ExistingPublicKey;
            PublicKeyBox.Text = key ?? string.Empty;
        }

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SshKeyViewModel.SelectedKey)
                               or nameof(SshKeyViewModel.ExistingPublicKey))
                Refresh();
        };

        Refresh();
    }

    private void InstallPasswordBox_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SshKeyViewModel vm)
            vm.InstallPassword = InstallPasswordBox.Password;
    }
}
