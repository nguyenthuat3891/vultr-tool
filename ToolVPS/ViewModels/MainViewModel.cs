using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ToolVPS.Services;

namespace ToolVPS.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly VultrService _vultr;

    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private object? _currentView;

    public InstanceListViewModel InstanceListVm { get; }
    public SshKeyViewModel SshKeyVm { get; }
    public DockerViewModel DockerVm { get; }

    public MainViewModel(VultrService vultr,
        InstanceListViewModel instanceListVm, SshKeyViewModel sshKeyVm,
        DockerViewModel dockerVm)
    {
        _vultr = vultr;
        InstanceListVm = instanceListVm;
        SshKeyVm = sshKeyVm;
        DockerVm = dockerVm;

        CurrentView = InstanceListVm;
    }

    [RelayCommand] private void ShowInstances() => CurrentView = InstanceListVm;
    [RelayCommand] private void ShowSshKeys()   => CurrentView = SshKeyVm;
    [RelayCommand] private void ShowDocker()    => CurrentView = DockerVm;
}
