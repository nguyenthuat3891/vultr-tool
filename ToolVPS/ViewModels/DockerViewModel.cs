using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ToolVPS.Models;
using ToolVPS.Services;

namespace ToolVPS.ViewModels;

public partial class DockerViewModel : ObservableObject
{
    private readonly DockerService _docker;
    private readonly SshService _ssh;

    // ── Docker state ──────────────────────────────────────────────────────────
    [ObservableProperty] private bool _dockerInstalled;
    [ObservableProperty] private string _dockerVersion = string.Empty;
    [ObservableProperty] private string _dockerInfo = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "Not checked yet. Click 'Check Docker'.";
    [ObservableProperty] private string _outputLog = string.Empty;

    // ── Containers ────────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<DockerContainer> _containers = new();
    [ObservableProperty] private DockerContainer? _selectedContainer;

    // ── Compose services ──────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<DockerComposeService> _composeServices = new();
    [ObservableProperty] private DockerComposeService? _selectedService;

    // ── Port tunnels ──────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<PortTunnel> _activeTunnels = new();
    [ObservableProperty] private string _tunnelLabel = "Postgres";
    [ObservableProperty] private uint _tunnelLocalPort = 5432;
    [ObservableProperty] private string _tunnelRemoteHost = "127.0.0.1";
    [ObservableProperty] private uint _tunnelRemotePort = 5432;

    public DockerViewModel(DockerService docker, SshService ssh)
    {
        _docker = docker;
        _ssh = ssh;
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task CheckDockerAsync()
    {
        if (!_ssh.IsConnected) { StatusMessage = "Not connected to SSH."; return; }
        IsBusy = true;
        StatusMessage = "Checking Docker...";
        await Task.Run(() =>
        {
            DockerInstalled = _docker.CheckDocker();
            DockerVersion = DockerInstalled ? _docker.GetDockerVersion() : "Docker not found.";
            DockerInfo = DockerInstalled ? _docker.GetDockerInfo() : string.Empty;
        });
        StatusMessage = DockerInstalled ? $"Docker found: {DockerVersion}" : "Docker is NOT installed on this server.";
        if (DockerInstalled) await RefreshAllAsync();
        IsBusy = false;
    }

    [RelayCommand]
    private async Task RefreshAllAsync()
    {
        if (!_ssh.IsConnected || !DockerInstalled) return;
        IsBusy = true;
        StatusMessage = "Refreshing...";
        List<DockerContainer> containers = new();
        List<DockerComposeService> services = new();
        await Task.Run(() =>
        {
            containers = _docker.GetContainers();
            services = _docker.GetComposeServices();
        });

        Containers.Clear();
        foreach (var c in containers) Containers.Add(c);

        ComposeServices.Clear();
        foreach (var s in services) ComposeServices.Add(s);

        StatusMessage = $"Containers: {containers.Count}  |  Compose services: {services.Count}";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task StartContainerAsync()
    {
        if (SelectedContainer is null) return;
        IsBusy = true;
        var output = await Task.Run(() => _docker.StartContainer(SelectedContainer.Id));
        AppendLog($"start {SelectedContainer.DisplayName}: {output}");
        await RefreshAllAsync();
    }

    [RelayCommand]
    private async Task StopContainerAsync()
    {
        if (SelectedContainer is null) return;
        IsBusy = true;
        var output = await Task.Run(() => _docker.StopContainer(SelectedContainer.Id));
        AppendLog($"stop {SelectedContainer.DisplayName}: {output}");
        await RefreshAllAsync();
    }

    [RelayCommand]
    private async Task RestartContainerAsync()
    {
        if (SelectedContainer is null) return;
        IsBusy = true;
        var output = await Task.Run(() => _docker.RestartContainer(SelectedContainer.Id));
        AppendLog($"restart {SelectedContainer.DisplayName}: {output}");
        await RefreshAllAsync();
    }

    [RelayCommand]
    private async Task ViewLogsAsync()
    {
        var id = SelectedContainer?.Id ?? SelectedService?.Name;
        if (string.IsNullOrEmpty(id)) { StatusMessage = "Select a container first."; return; }
        IsBusy = true;
        StatusMessage = "Fetching logs...";
        var logs = await Task.Run(() => _docker.GetContainerLogs(id, 150));
        OutputLog = logs;
        StatusMessage = "Logs loaded.";
        IsBusy = false;
    }

    // ── Port tunnel commands ──────────────────────────────────────────────────

    [RelayCommand]
    private void AddTunnel()
    {
        if (!_ssh.IsConnected) { StatusMessage = "SSH not connected."; return; }
        try
        {
            _ssh.StartTunnel(TunnelLocalPort, TunnelRemoteHost, TunnelRemotePort);
            var t = new PortTunnel
            {
                Label = TunnelLabel,
                LocalPort = TunnelLocalPort,
                RemoteHost = TunnelRemoteHost,
                RemotePort = TunnelRemotePort,
                IsActive = true
            };
            ActiveTunnels.Add(t);
            StatusMessage = $"Tunnel active: localhost:{TunnelLocalPort} → {TunnelRemoteHost}:{TunnelRemotePort}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Tunnel error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RemoveTunnel(PortTunnel? tunnel)
    {
        if (tunnel is null) return;
        try
        {
            _ssh.StopTunnel(tunnel.LocalPort);
            ActiveTunnels.Remove(tunnel);
            StatusMessage = $"Tunnel removed: localhost:{tunnel.LocalPort}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void QuickPostgresTunnel()
    {
        TunnelLabel = "Postgres";
        TunnelLocalPort = 5432;
        TunnelRemoteHost = "127.0.0.1";
        TunnelRemotePort = 5432;
        AddTunnel();
    }

    [RelayCommand]
    private void QuickMysqlTunnel()
    {
        TunnelLabel = "MySQL";
        TunnelLocalPort = 3306;
        TunnelRemoteHost = "127.0.0.1";
        TunnelRemotePort = 3306;
        AddTunnel();
    }

    [RelayCommand]
    private void QuickRedisTunnel()
    {
        TunnelLabel = "Redis";
        TunnelLocalPort = 6379;
        TunnelRemoteHost = "127.0.0.1";
        TunnelRemotePort = 6379;
        AddTunnel();
    }

    private void AppendLog(string text)
    {
        OutputLog += $"\n[{DateTime.Now:HH:mm:ss}] {text}";
    }
}
