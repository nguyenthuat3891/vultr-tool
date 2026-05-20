using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Renci.SshNet;
using ToolVPS.Models;
using ToolVPS.Services;

namespace ToolVPS.ViewModels;

public partial class TerminalViewModel : ObservableObject, IDisposable
{
    private static readonly Regex AnsiRegex = new(
        @"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~]|\][^\a\x1B]*(?:\a|\x1B\\))",
        RegexOptions.Compiled);
    private const int MaxTerminalChars = 200_000;

    private readonly SshService _ssh;
    private readonly SettingsService _settings;
    private readonly StringBuilder _terminalBuffer = new(50_000);
    private ShellStream? _shell;
    private Thread? _readThread;
    private volatile bool _reading;

    [ObservableProperty]
    private string _host = string.Empty;

    [ObservableProperty]
    private int _port = 22;

    [ObservableProperty]
    private string _username = "root";

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _privateKeyPath = string.Empty;

    [ObservableProperty]
    private bool _usePassword = true;

    [ObservableProperty]
    private string _terminalOutput = string.Empty;

    [ObservableProperty]
    private string _commandInput = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _statusMessage = "Disconnected";

    [ObservableProperty]
    private ObservableCollection<RemoteFileEntry> _fileEntries = new();

    [ObservableProperty]
    private string _currentRemotePath = "/root";

    [ObservableProperty]
    private RemoteFileEntry? _selectedFile;

    [ObservableProperty]
    private string _fileEditorContent = string.Empty;

    [ObservableProperty]
    private string _editingFilePath = string.Empty;

    [ObservableProperty]
    private string _newItemName = string.Empty;

    public event Action<string>? TerminalOutputReceived;
    public event Action? TerminalCleared;

    public TerminalViewModel(SshService ssh, SettingsService settings)
    {
        _ssh = ssh;
        _settings = settings;
        RestoreLastConnection();
    }

    private void RestoreLastConnection()
    {
        var last = _settings.Settings.LastSshConnection;
        if (last is null) return;
        Host = last.Host;
        Port = last.Port;
        Username = last.Username;
        UsePassword = last.UsePassword;
        PrivateKeyPath = last.PrivateKeyPath;
    }

    private void SaveLastConnection()
    {
        _settings.Settings.LastSshConnection = new Models.LastSshConnection
        {
            Host = Host,
            Port = Port,
            Username = Username,
            UsePassword = UsePassword,
            PrivateKeyPath = PrivateKeyPath
        };
        _settings.Save();
    }

    public void PreFill(VultrInstance instance)
    {
        Host = instance.MainIp;
        StatusMessage = $"Ready to connect to {instance.DisplayLabel} ({instance.MainIp})";
    }

    public void AutoConnect(VultrInstance instance, string username, string password, string keyPath)
    {
        AutoConnect(instance.MainIp, 22, username, password, keyPath);
    }

    public void AutoConnect(string host, int port, string username, string password, string keyPath)
    {
        if (IsConnected)
        {
            StopShell();
            _ssh.Disconnect();
            IsConnected = false;
        }

        Host = host;
        Port = port;
        Username = username;

        if (!string.IsNullOrWhiteSpace(keyPath))
        {
            UsePassword = false;
            PrivateKeyPath = keyPath;
        }
        else
        {
            UsePassword = true;
            Password = password;
        }

        ConnectCommand.Execute(null);
    }

    [RelayCommand]
    private void Connect()
    {
        if (string.IsNullOrWhiteSpace(Host)) { StatusMessage = "Host is required."; return; }
        try
        {
            if (UsePassword)
                _ssh.ConnectWithPassword(Host, Port, Username, Password);
            else
                _ssh.Connect(Host, Port, Username, PrivateKeyPath);

            IsConnected = true;
            StatusMessage = $"Connected to {Host}";
            SaveLastConnection();
            StartShell();
            RefreshFiles();
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (msg.Contains("Permission denied", StringComparison.OrdinalIgnoreCase))
            {
                if (!UsePassword)
                    StatusMessage = "Permission denied (publickey) — The public key is not installed on the server. " +
                                    "Go to SSH Keys tab → Load Existing Key → browse your private key → " +
                                    "fill in host + password → click 'Install Public Key'.";
                else
                    StatusMessage = "Permission denied (password) — Wrong password, or the server has password auth disabled. " +
                                    "Try using a private key instead.";
            }
            else
            {
                StatusMessage = $"Connect error: {msg}";
            }
        }
    }

    [RelayCommand]
    private void Disconnect()
    {
        StopShell();
        _ssh.Disconnect();
        IsConnected = false;
        StatusMessage = "Disconnected";
    }

    [RelayCommand]
    private void SendCommand()
    {
        if (!IsConnected || _shell == null || string.IsNullOrEmpty(CommandInput)) return;
        _shell.WriteLine(CommandInput);
        CommandInput = string.Empty;
    }

    [RelayCommand]
    private void RefreshFiles()
    {
        if (!IsConnected) return;
        try
        {
            var entries = _ssh.ListDirectory(CurrentRemotePath);
            FileEntries.Clear();
            foreach (var e in entries)
                FileEntries.Add(e);
        }
        catch (Exception ex)
        {
            StatusMessage = $"File list error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenFileOrDirectory()
    {
        if (SelectedFile is null) return;

        if (SelectedFile.IsDirectory)
        {
            var path = SelectedFile.FullPath;
            CurrentRemotePath = path;
            RefreshFiles();
            _shell?.WriteLine($"cd {path}");
            return;
        }

        try
        {
            FileEditorContent = _ssh.ReadFile(SelectedFile.FullPath);
            EditingFilePath = SelectedFile.FullPath;
            StatusMessage = $"Opened: {SelectedFile.FullPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Open error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CreateDirectory()
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(NewItemName)) return;
        try
        {
            var path = CurrentRemotePath.TrimEnd('/') + "/" + NewItemName.Trim();
            _ssh.CreateDirectory(path);
            NewItemName = string.Empty;
            RefreshFiles();
            StatusMessage = $"Created folder: {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Create folder error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CreateFile()
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(NewItemName)) return;
        try
        {
            var path = CurrentRemotePath.TrimEnd('/') + "/" + NewItemName.Trim();
            _ssh.CreateFile(path);
            NewItemName = string.Empty;
            RefreshFiles();
            StatusMessage = $"Created file: {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Create file error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SaveFile()
    {
        if (string.IsNullOrEmpty(EditingFilePath)) return;
        try
        {
            _ssh.WriteFile(EditingFilePath, FileEditorContent);
            StatusMessage = $"Saved: {EditingFilePath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void NavigateUp()
    {
        var parent = System.IO.Path.GetDirectoryName(CurrentRemotePath.TrimEnd('/'));
        if (!string.IsNullOrEmpty(parent))
        {
            CurrentRemotePath = parent.Replace('\\', '/');
            RefreshFiles();
        }
    }

    private void AppendTerminalData(string raw)
    {
        var text = AnsiRegex.Replace(raw, string.Empty).Replace("\r\n", "\n");

        foreach (char c in text)
        {
            switch (c)
            {
                case '\r':
                    int lastNl = -1;
                    for (int j = _terminalBuffer.Length - 1; j >= 0; j--)
                    {
                        if (_terminalBuffer[j] == '\n') { lastNl = j; break; }
                    }
                    int lineStart = lastNl + 1;
                    if (_terminalBuffer.Length > lineStart)
                        _terminalBuffer.Remove(lineStart, _terminalBuffer.Length - lineStart);
                    break;
                case '\b':
                    if (_terminalBuffer.Length > 0 && _terminalBuffer[_terminalBuffer.Length - 1] != '\n')
                        _terminalBuffer.Remove(_terminalBuffer.Length - 1, 1);
                    break;
                case '\0':
                case '\a':
                    break;
                default:
                    _terminalBuffer.Append(c);
                    break;
            }
        }

        if (_terminalBuffer.Length > MaxTerminalChars)
        {
            int target = _terminalBuffer.Length - MaxTerminalChars / 2;
            for (int j = target; j < _terminalBuffer.Length; j++)
            {
                if (_terminalBuffer[j] != '\n') continue;
                _terminalBuffer.Remove(0, j + 1);
                break;
            }
        }

        TerminalOutput = _terminalBuffer.ToString();
    }

    private void StartShell()
    {
        _terminalBuffer.Clear();
        TerminalOutput = string.Empty;
        _shell = _ssh.OpenShell();
        _shell.WriteLine("export PAGER=cat MANPAGER=cat");
        _reading = true;
        _readThread = new Thread(() =>
        {
            while (_reading && _shell != null)
            {
                try
                {
                    var data = _shell.Read();
                    if (!string.IsNullOrEmpty(data))
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            AppendTerminalData(data);
                            TerminalOutputReceived?.Invoke(data);
                        });
                    Thread.Sleep(50);
                }
                catch { break; }
            }
        }) { IsBackground = true };
        _readThread.Start();
    }

    private void StopShell()
    {
        _reading = false;
        _shell?.Dispose();
        _shell = null;
    }

    public void Dispose()
    {
        StopShell();
        _ssh.Dispose();
    }
}
