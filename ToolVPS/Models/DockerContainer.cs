using System.Text.Json.Serialization;

namespace ToolVPS.Models;

public class DockerContainer
{
    [JsonPropertyName("ID")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("Names")]
    public string Names { get; set; } = string.Empty;

    [JsonPropertyName("Image")]
    public string Image { get; set; } = string.Empty;

    [JsonPropertyName("State")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("Status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("Ports")]
    public string Ports { get; set; } = string.Empty;

    [JsonPropertyName("CreatedAt")]
    public string CreatedAt { get; set; } = string.Empty;

    public bool IsRunning => State.Equals("running", StringComparison.OrdinalIgnoreCase);
    public string ShortId => Id.Length > 12 ? Id[..12] : Id;
    public string DisplayName => Names.TrimStart('/');
    public string StateIcon => IsRunning ? "▶" : "■";
    public string StateColor => IsRunning ? "#16A34A" : "#DC2626";
}

public class DockerComposeService
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Service")]
    public string Service { get; set; } = string.Empty;

    [JsonPropertyName("Status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("Publishers")]
    public List<DockerPublisher>? Publishers { get; set; }

    public bool IsRunning => Status.Contains("running", StringComparison.OrdinalIgnoreCase)
                          || Status.Contains("Up", StringComparison.OrdinalIgnoreCase);
    public string StateIcon => IsRunning ? "▶" : "■";
    public string Ports => Publishers != null
        ? string.Join(", ", Publishers.Select(p => p.DisplayPort))
        : string.Empty;
}

public class DockerPublisher
{
    [JsonPropertyName("URL")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("TargetPort")]
    public int TargetPort { get; set; }

    [JsonPropertyName("PublishedPort")]
    public int PublishedPort { get; set; }

    [JsonPropertyName("Protocol")]
    public string Protocol { get; set; } = string.Empty;

    public string DisplayPort => PublishedPort > 0
        ? $"{PublishedPort}→{TargetPort}"
        : TargetPort.ToString();
}

public class PortTunnel
{
    public string Label { get; set; } = string.Empty;
    public uint LocalPort { get; set; }
    public string RemoteHost { get; set; } = "127.0.0.1";
    public uint RemotePort { get; set; }
    public bool IsActive { get; set; }

    public string DisplayText => $"localhost:{LocalPort} → {RemoteHost}:{RemotePort}  ({Label})";
}
