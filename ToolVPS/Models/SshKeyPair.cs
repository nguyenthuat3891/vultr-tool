namespace ToolVPS.Models;

public class SshKeyPair
{
    public string Name { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKeyPath { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
}
