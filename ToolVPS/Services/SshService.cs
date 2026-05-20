using System.IO;
using System.Text;
using Renci.SshNet;
using ToolVPS.Models;

namespace ToolVPS.Services;

public class SshService : IDisposable
{
    private SshClient? _sshClient;
    private SftpClient? _sftpClient;
    private ShellStream? _shellStream;
    private readonly Dictionary<uint, ForwardedPortLocal> _tunnels = new();

    public bool IsConnected => _sshClient?.IsConnected == true;

    public SshKeyPair GenerateKeyPair(string keyName, string saveDirectory, string keyType = "RSA")
    {
        Directory.CreateDirectory(saveDirectory);
        return keyType == "Ed25519"
            ? GenerateEd25519KeyPair(keyName, saveDirectory)
            : GenerateRsaKeyPair(keyName, saveDirectory);
    }

    private static SshKeyPair GenerateRsaKeyPair(string keyName, string saveDirectory)
    {
        using var rsa = System.Security.Cryptography.RSA.Create(4096);
        var privateKeyPem = rsa.ExportRSAPrivateKeyPem();
        var privateKeyPath = Path.Combine(saveDirectory, keyName);
        File.WriteAllText(privateKeyPath, privateKeyPem);
        var sshPublicKey = BuildSshRsaPublicKey(rsa, keyName);
        File.WriteAllText(privateKeyPath + ".pub", sshPublicKey);
        return new SshKeyPair { Name = keyName, PrivateKeyPath = privateKeyPath, PublicKey = sshPublicKey, GeneratedAt = DateTime.UtcNow };
    }

    private static SshKeyPair GenerateEd25519KeyPair(string keyName, string saveDirectory)
    {
        var generator = new Org.BouncyCastle.Crypto.Generators.Ed25519KeyPairGenerator();
        generator.Init(new Org.BouncyCastle.Crypto.Parameters.Ed25519KeyGenerationParameters(new Org.BouncyCastle.Security.SecureRandom()));
        var keyPair = generator.GenerateKeyPair();

        var privSeed = ((Org.BouncyCastle.Crypto.Parameters.Ed25519PrivateKeyParameters)keyPair.Private).GetEncoded();
        var pubBytes = ((Org.BouncyCastle.Crypto.Parameters.Ed25519PublicKeyParameters)keyPair.Public).GetEncoded();

        var privateKeyPem = BuildOpenSshEd25519PrivateKey(privSeed, pubBytes, keyName);
        var publicKeyStr  = BuildSshEd25519PublicKey(pubBytes, keyName);
        var privateKeyPath = Path.Combine(saveDirectory, keyName);
        File.WriteAllText(privateKeyPath, privateKeyPem);
        File.WriteAllText(privateKeyPath + ".pub", publicKeyStr);
        return new SshKeyPair { Name = keyName, PrivateKeyPath = privateKeyPath, PublicKey = publicKeyStr, GeneratedAt = DateTime.UtcNow };
    }

    private static string BuildOpenSshEd25519PrivateKey(byte[] privSeed, byte[] pubBytes, string comment)
    {
        var fullPrivKey = new byte[64];
        Buffer.BlockCopy(privSeed, 0, fullPrivKey, 0, 32);
        Buffer.BlockCopy(pubBytes, 0, fullPrivKey, 32, 32);

        using var ms = new MemoryStream();
        ms.Write(Encoding.ASCII.GetBytes("openssh-key-v1"));
        ms.WriteByte(0);
        WriteSshString(ms, "none"); // cipher
        WriteSshString(ms, "none"); // kdf
        WriteSshUint32(ms, 0);      // kdf options (empty)
        WriteSshUint32(ms, 1);      // number of keys

        using var pubMs = new MemoryStream();
        WriteSshString(pubMs, "ssh-ed25519");
        WriteSshBlob(pubMs, pubBytes);
        WriteSshBlob(ms, pubMs.ToArray());

        var checkBytes = new byte[4];
        System.Security.Cryptography.RandomNumberGenerator.Fill(checkBytes);
        using var privMs = new MemoryStream();
        privMs.Write(checkBytes);
        privMs.Write(checkBytes);
        WriteSshString(privMs, "ssh-ed25519");
        WriteSshBlob(privMs, pubBytes);
        WriteSshBlob(privMs, fullPrivKey);
        WriteSshString(privMs, comment);
        for (byte pad = 1; privMs.Length % 8 != 0; pad++)
            privMs.WriteByte(pad);
        WriteSshBlob(ms, privMs.ToArray());

        var base64 = Convert.ToBase64String(ms.ToArray());
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN OPENSSH PRIVATE KEY-----");
        for (int i = 0; i < base64.Length; i += 70)
            sb.AppendLine(base64.Substring(i, Math.Min(70, base64.Length - i)));
        sb.Append("-----END OPENSSH PRIVATE KEY-----");
        return sb.ToString();
    }

    private static string BuildSshEd25519PublicKey(byte[] pubBytes, string comment)
    {
        using var ms = new MemoryStream();
        WriteSshString(ms, "ssh-ed25519");
        WriteSshBlob(ms, pubBytes);
        return $"ssh-ed25519 {Convert.ToBase64String(ms.ToArray())} {comment}";
    }

    private static void WriteSshBlob(Stream s, byte[] data)
    {
        WriteSshUint32(s, (uint)data.Length);
        s.Write(data);
    }

    // Encodes an RSA public key in the SSH wire format used by authorized_keys
    private static string BuildSshRsaPublicKey(
        System.Security.Cryptography.RSA rsa, string comment)
    {
        var p = rsa.ExportParameters(false);
        using var ms = new MemoryStream();
        WriteSshString(ms, "ssh-rsa");
        WriteSshMpint(ms, p.Exponent!);
        WriteSshMpint(ms, p.Modulus!);
        return $"ssh-rsa {Convert.ToBase64String(ms.ToArray())} {comment}";
    }

    private static void WriteSshString(Stream s, string value)
    {
        var b = Encoding.ASCII.GetBytes(value);
        WriteSshUint32(s, (uint)b.Length);
        s.Write(b);
    }

    private static void WriteSshMpint(Stream s, byte[] data)
    {
        // Strip leading zeros
        int start = 0;
        while (start < data.Length - 1 && data[start] == 0) start++;
        bool pad = (data[start] & 0x80) != 0;
        WriteSshUint32(s, (uint)(data.Length - start + (pad ? 1 : 0)));
        if (pad) s.WriteByte(0);
        s.Write(data, start, data.Length - start);
    }

    private static void WriteSshUint32(Stream s, uint v)
    {
        s.WriteByte((byte)(v >> 24));
        s.WriteByte((byte)(v >> 16));
        s.WriteByte((byte)(v >> 8));
        s.WriteByte((byte)v);
    }

    // Derives the correct SSH public key from a private key file (RSA/PKCS#1 or PKCS#8)
    // Returns null if the key type is not RSA (e.g. Ed25519 — use the .pub file for those)
    public string? ExtractPublicKeyFromPrivateKey(string privateKeyPath, string comment = "")
    {
        try
        {
            var pem = File.ReadAllText(privateKeyPath);
            using var rsa = System.Security.Cryptography.RSA.Create();

            if (pem.Contains("BEGIN RSA PRIVATE KEY", StringComparison.Ordinal))
                rsa.ImportFromPem(pem);          // PKCS#1
            else if (pem.Contains("BEGIN PRIVATE KEY", StringComparison.Ordinal))
                rsa.ImportFromPem(pem);          // PKCS#8
            else if (pem.Contains("BEGIN OPENSSH PRIVATE KEY", StringComparison.Ordinal))
                rsa.ImportFromPem(pem);          // OpenSSH (works for RSA only)
            else
                return null;                     // Ed25519 / ECDSA — not RSA

            return BuildSshRsaPublicKey(rsa, string.IsNullOrWhiteSpace(comment)
                ? Path.GetFileName(privateKeyPath)
                : comment);
        }
        catch { return null; }
    }

    // Install a public key on the remote server (like ssh-copy-id)
    public (bool ok, string message) InstallPublicKey(
        string host, int port, string username, string password, string publicKeyText)
    {
        try
        {
            var auth = new PasswordAuthenticationMethod(username, password);
            var info = new ConnectionInfo(host, port, username, auth);
            using var client = new SshClient(info);
            client.Connect();

            var escaped = publicKeyText.Replace("'", "'\\''");
            var cmd = client.CreateCommand(
                $"mkdir -p ~/.ssh && " +
                $"chmod 700 ~/.ssh && " +
                $"echo '{escaped}' >> ~/.ssh/authorized_keys && " +
                $"chmod 600 ~/.ssh/authorized_keys && " +
                $"echo OK");
            var result = cmd.Execute().Trim();
            client.Disconnect();
            return result == "OK"
                ? (true,  "Public key installed successfully. You can now connect with the private key.")
                : (false, $"Command output: {result}\nError: {cmd.Error}");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to install key: {ex.Message}");
        }
    }

    public void Connect(string host, int port, string username, string privateKeyPath)
    {
        Disconnect();

        var keyFile = new PrivateKeyFile(privateKeyPath);
        var authMethod = new PrivateKeyAuthenticationMethod(username, keyFile);
        var connectionInfo = new ConnectionInfo(host, port, username, authMethod);

        _sshClient = new SshClient(connectionInfo);
        _sshClient.Connect();

        _sftpClient = new SftpClient(connectionInfo);
        _sftpClient.Connect();
    }

    public void ConnectWithPassword(string host, int port, string username, string password)
    {
        Disconnect();

        var authMethod = new PasswordAuthenticationMethod(username, password);
        var connectionInfo = new ConnectionInfo(host, port, username, authMethod);

        _sshClient = new SshClient(connectionInfo);
        _sshClient.Connect();

        _sftpClient = new SftpClient(connectionInfo);
        _sftpClient.Connect();
    }

    public string ExecuteCommand(string command)
    {
        if (_sshClient == null || !_sshClient.IsConnected)
            throw new InvalidOperationException("Not connected.");

        using var cmd = _sshClient.CreateCommand(command);
        cmd.Execute();
        return string.IsNullOrEmpty(cmd.Error) ? cmd.Result : cmd.Error;
    }

    public ShellStream OpenShell()
    {
        if (_sshClient == null || !_sshClient.IsConnected)
            throw new InvalidOperationException("Not connected.");

        _shellStream = _sshClient.CreateShellStream("dumb", 220, 50, 800, 600, 4096);
        return _shellStream;
    }

    public List<RemoteFileEntry> ListDirectory(string remotePath)
    {
        if (_sftpClient == null || !_sftpClient.IsConnected)
            throw new InvalidOperationException("SFTP not connected.");

        return _sftpClient.ListDirectory(remotePath)
            .Where(f => f.Name != ".")
            .Select(f => new RemoteFileEntry
            {
                Name = f.Name,
                FullPath = f.FullName,
                IsDirectory = f.IsDirectory,
                Size = f.Length,
                LastModified = f.LastWriteTime
            })
            .OrderByDescending(f => f.IsDirectory)
            .ThenBy(f => f.Name)
            .ToList();
    }

    public void CreateDirectory(string remotePath)
    {
        if (_sftpClient == null || !_sftpClient.IsConnected)
            throw new InvalidOperationException("SFTP not connected.");
        _sftpClient.CreateDirectory(remotePath);
    }

    public void CreateFile(string remotePath)
    {
        if (_sftpClient == null || !_sftpClient.IsConnected)
            throw new InvalidOperationException("SFTP not connected.");
        using var stream = _sftpClient.Create(remotePath);
    }

    public string ReadFile(string remotePath)
    {
        if (_sftpClient == null || !_sftpClient.IsConnected)
            throw new InvalidOperationException("SFTP not connected.");

        using var stream = _sftpClient.OpenRead(remotePath);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public void WriteFile(string remotePath, string content)
    {
        if (_sftpClient == null || !_sftpClient.IsConnected)
            throw new InvalidOperationException("SFTP not connected.");

        using var stream = _sftpClient.OpenWrite(remotePath);
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
        stream.SetLength(bytes.Length);
    }

    // ── Port forwarding ──────────────────────────────────────────────────────

    public void StartTunnel(uint localPort, string remoteHost, uint remotePort)
    {
        if (_sshClient == null || !_sshClient.IsConnected)
            throw new InvalidOperationException("Not connected.");

        if (_tunnels.ContainsKey(localPort))
            StopTunnel(localPort);

        var fwd = new ForwardedPortLocal("127.0.0.1", localPort, remoteHost, remotePort);
        _sshClient.AddForwardedPort(fwd);
        fwd.Start();
        _tunnels[localPort] = fwd;
    }

    public void StopTunnel(uint localPort)
    {
        if (!_tunnels.TryGetValue(localPort, out var fwd)) return;
        if (fwd.IsStarted) fwd.Stop();
        _sshClient?.RemoveForwardedPort(fwd);
        fwd.Dispose();
        _tunnels.Remove(localPort);
    }

    public IReadOnlyDictionary<uint, ForwardedPortLocal> ActiveTunnels => _tunnels;

    // ─────────────────────────────────────────────────────────────────────────

    public void Disconnect()
    {
        foreach (var key in _tunnels.Keys.ToList())
            StopTunnel(key);

        _shellStream?.Dispose();
        _shellStream = null;

        if (_sftpClient?.IsConnected == true)
            _sftpClient.Disconnect();
        _sftpClient?.Dispose();
        _sftpClient = null;

        if (_sshClient?.IsConnected == true)
            _sshClient.Disconnect();
        _sshClient?.Dispose();
        _sshClient = null;
    }

    public void Dispose() => Disconnect();
}

public class RemoteFileEntry
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public DateTime LastModified { get; set; }

    public string SizeDisplay => IsDirectory ? "<DIR>" : FormatSize(Size);

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1048576) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / 1048576.0:F1} MB";
    }
}
