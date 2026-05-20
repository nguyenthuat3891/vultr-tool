using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ToolVPS.Models;
using ToolVPS.Services;

namespace ToolVPS.ViewModels;

public partial class InstanceListViewModel : ObservableObject
{
    private readonly VultrService _vultr;

    [ObservableProperty]
    private ObservableCollection<VultrInstance> _instances = new();

    [ObservableProperty]
    private VultrInstance? _selectedInstance;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _localSshUsername = "root";

    [ObservableProperty]
    private string _localSshKeyPath = string.Empty;

    [ObservableProperty]
    private string _quickUsername = "root";

    [ObservableProperty]
    private string _quickKeyPath = string.Empty;

    [ObservableProperty]
    private string _quickHost = string.Empty;

    [ObservableProperty]
    private string _quickPort = "22";

    // Password is not bindable on PasswordBox; code-behind sets this
    public string QuickPassword { get; set; } = string.Empty;

    public event Action<VultrInstance>? ConnectRequested;
    public event Action<string, int, string, string, string>? QuickConnectRequested;

    public InstanceListViewModel(VultrService vultr)
    {
        _vultr = vultr;
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        IsBusy = true;
        StatusMessage = "Fetching instances...";
        try
        {
            var list = await _vultr.GetInstancesAsync(ct);
            Instances.Clear();
            foreach (var inst in list)
                Instances.Add(inst);
            StatusMessage = $"Loaded {list.Count} instance(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RebootAsync()
    {
        if (SelectedInstance is null) return;
        IsBusy = true;
        StatusMessage = $"Rebooting {SelectedInstance.DisplayLabel}...";
        try
        {
            var ok = await _vultr.RebootInstanceAsync(SelectedInstance.Id);
            StatusMessage = ok ? "Reboot command sent." : "Reboot failed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Connect()
    {
        if (SelectedInstance is null) return;
        ConnectRequested?.Invoke(SelectedInstance);
    }

    [RelayCommand]
    private void QuickConnect()
    {
        var host = !string.IsNullOrWhiteSpace(QuickHost)
            ? QuickHost.Trim()
            : SelectedInstance?.MainIp;

        if (string.IsNullOrWhiteSpace(host))
        {
            StatusMessage = "Enter an instance IP or select an instance first.";
            return;
        }

        if (!int.TryParse(QuickPort, out var port) || port <= 0)
        {
            StatusMessage = "Enter a valid SSH port number.";
            return;
        }

        QuickConnectRequested?.Invoke(host, port, QuickUsername, QuickPassword, QuickKeyPath);
    }

    [RelayCommand]
    private void BrowseQuickKey()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Select SSH Private Key  (NOT the .pub file)",
            Filter = "All Files (*.*)|*.*",
            InitialDirectory = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh")
        };
        if (dialog.ShowDialog() != true) return;
        if (dialog.FileName.EndsWith(".pub", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "Wrong file — you picked the PUBLIC key (.pub). Select the PRIVATE key (e.g. id_ed25519, no extension).";
            return;
        }
        QuickKeyPath = dialog.FileName;
    }

    [RelayCommand]
    private void OpenLocalSsh()
    {
        if (SelectedInstance is null) { StatusMessage = "Select an instance first."; return; }

        var ip = SelectedInstance.MainIp;
        var user = string.IsNullOrWhiteSpace(LocalSshUsername) ? "root" : LocalSshUsername;

        var sshArgs = string.IsNullOrWhiteSpace(LocalSshKeyPath)
            ? $"{user}@{ip}"
            : $"-i \"{LocalSshKeyPath}\" {user}@{ip}";

        // Try Windows Terminal first, fall back to cmd
        if (TryLaunch("wt.exe", $"ssh {sshArgs}")) { }
        else TryLaunch("cmd.exe", $"/k ssh {sshArgs}");

        StatusMessage = $"Opening local SSH: {user}@{ip}";
    }

    [RelayCommand]
    private void BrowseLocalSshKey()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Select SSH Private Key  (NOT the .pub file)",
            Filter = "All Files (*.*)|*.*",
            InitialDirectory = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh")
        };
        if (dialog.ShowDialog() != true) return;
        if (dialog.FileName.EndsWith(".pub", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "Wrong file — that is the PUBLIC key (.pub). Select the PRIVATE key (e.g. id_ed25519).";
            return;
        }
        LocalSshKeyPath = dialog.FileName;
    }

    private static bool TryLaunch(string exe, string args)
    {
        try
        {
            Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true });
            return true;
        }
        catch { return false; }
    }
}
