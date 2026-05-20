using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ToolVPS.Models;
using ToolVPS.Services;

namespace ToolVPS.ViewModels;

public partial class SshKeyViewModel : ObservableObject
{
    private readonly SshService _ssh;

    // ── Key generation ────────────────────────────────────────────────────────
    [ObservableProperty] private string _keyName = "id_ed25519";
    [ObservableProperty] private string _keyType = "Ed25519";
    public string[] KeyTypes { get; } = { "Ed25519", "RSA" };
    [ObservableProperty] private string _saveDirectory;
    [ObservableProperty] private ObservableCollection<SshKeyPair> _generatedKeys = new();
    [ObservableProperty] private SshKeyPair? _selectedKey;

    // ── Install key to server (ssh-copy-id) ───────────────────────────────────
    [ObservableProperty] private string _installHost = string.Empty;
    [ObservableProperty] private int    _installPort = 22;
    [ObservableProperty] private string _installUser = "root";
    public string InstallPassword { get; set; } = string.Empty; // set from code-behind

    // ── Browse an existing private key ────────────────────────────────────────
    [ObservableProperty] private string _existingKeyPath = string.Empty;
    [ObservableProperty] private string _existingPublicKey = string.Empty;

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool   _isBusy;

    public SshKeyViewModel(SshService ssh)
    {
        _ssh = ssh;
        _saveDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
    }

    // ── Generate ──────────────────────────────────────────────────────────────
    [RelayCommand]
    private void GenerateKey()
    {
        if (string.IsNullOrWhiteSpace(KeyName))
        {
            StatusMessage = "Key name cannot be empty.";
            return;
        }
        try
        {
            var pair = _ssh.GenerateKeyPair(KeyName, SaveDirectory, KeyType);
            GeneratedKeys.Add(pair);
            SelectedKey = pair;
            StatusMessage = $"Key pair generated: {pair.PrivateKeyPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CopyPublicKey()
    {
        var pubKey = SelectedKey?.PublicKey ?? ExistingPublicKey;
        if (string.IsNullOrWhiteSpace(pubKey)) { StatusMessage = "No public key to copy."; return; }
        System.Windows.Clipboard.SetText(pubKey);
        StatusMessage = "Public key copied to clipboard.";
    }

    [RelayCommand]
    private void BrowseSaveDirectory()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            SelectedPath = SaveDirectory,
            Description  = "Select directory to save SSH keys"
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            SaveDirectory = dialog.SelectedPath;
    }

    // ── Load existing key from disk ───────────────────────────────────────────
    [RelayCommand]
    private void BrowseExistingKey()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Select SSH Private Key  (NOT the .pub file)",
            Filter = "All Files (*.*)|*.*",
            InitialDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh")
        };
        if (dialog.ShowDialog() != true) return;

        var path = dialog.FileName;

        if (path.EndsWith(".pub", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "You selected the PUBLIC key (.pub) — please select the PRIVATE key (no extension, e.g. id_ed25519 or new-nguyenthuat-ssh).";
            return;
        }

        ExistingKeyPath = path;

        // Always re-derive the public key from the private key (fixes broken .pub files)
        var derived = _ssh.ExtractPublicKeyFromPrivateKey(path, Path.GetFileName(path));
        if (derived != null)
        {
            ExistingPublicKey = derived;
            // Also overwrite the .pub file on disk with the correct format
            File.WriteAllText(path + ".pub", derived);
            StatusMessage = $"RSA key loaded. Public key derived from private key and saved to {Path.GetFileName(path)}.pub  ✓";
            return;
        }

        // Not RSA (e.g. Ed25519 from ssh-keygen) — read the .pub file as-is
        var pubPath = path + ".pub";
        if (File.Exists(pubPath))
        {
            var pubContent = File.ReadAllText(pubPath).Trim();
            // Basic sanity check: must start with a known key type
            if (pubContent.StartsWith("ssh-") || pubContent.StartsWith("ecdsa-"))
            {
                ExistingPublicKey = pubContent;
                StatusMessage = $"Key loaded ({Path.GetFileName(path)}).  " +
                                $"Public key read from {Path.GetFileName(pubPath)}  ✓";
            }
            else
            {
                ExistingPublicKey = string.Empty;
                StatusMessage = $"WARNING: {Path.GetFileName(pubPath)} appears to be in wrong format. " +
                                $"Use 'Install Key → Server' only after regenerating the key pair.";
            }
        }
        else
        {
            ExistingPublicKey = string.Empty;
            StatusMessage = $"No matching .pub file found for {Path.GetFileName(path)}. " +
                            $"Generate a new key pair to get a valid public key.";
        }
    }

    // ── Install key to server (ssh-copy-id equivalent) ────────────────────────
    [RelayCommand]
    private async Task InstallKeyAsync()
    {
        var pubKey = SelectedKey?.PublicKey ?? ExistingPublicKey;
        if (string.IsNullOrWhiteSpace(pubKey))
        {
            StatusMessage = "No public key selected. Generate or browse a key first.";
            return;
        }
        if (string.IsNullOrWhiteSpace(InstallHost))
        {
            StatusMessage = "Enter the server host/IP.";
            return;
        }
        if (string.IsNullOrWhiteSpace(InstallPassword))
        {
            StatusMessage = "Enter the server password (needed once to install the key).";
            return;
        }

        IsBusy = true;
        StatusMessage = $"Installing key on {InstallUser}@{InstallHost}…";
        var (ok, msg) = await Task.Run(() =>
            _ssh.InstallPublicKey(InstallHost, InstallPort, InstallUser, InstallPassword, pubKey));
        StatusMessage = msg;
        IsBusy = false;
    }
}
