namespace ToolVPS.Models;

public class AppSettings
{
    public string VultrApiKey { get; set; } = string.Empty;
    public List<SavedServer> SavedServers { get; set; } = new();
    public LastSshConnection? LastSshConnection { get; set; }
    public LastQuickConnect? LastQuickConnect { get; set; }
}

public class LastSshConnection
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = "root";
    public bool UsePassword { get; set; } = true;
    public string PrivateKeyPath { get; set; } = string.Empty;
}

public class LastQuickConnect
{
    public string Host { get; set; } = string.Empty;
    public string Port { get; set; } = "22";
    public string Username { get; set; } = "root";
    public string KeyPath { get; set; } = string.Empty;
}

public class SavedServer
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string PrivateKeyPath { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}
