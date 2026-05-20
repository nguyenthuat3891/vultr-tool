using System.Text.Json.Serialization;

namespace ToolVPS.Models;

public class VultrInstance
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("main_ip")]
    public string MainIp { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("region")]
    public string Region { get; set; } = string.Empty;

    [JsonPropertyName("plan")]
    public string Plan { get; set; } = string.Empty;

    [JsonPropertyName("os")]
    public string Os { get; set; } = string.Empty;

    [JsonPropertyName("ram")]
    public int Ram { get; set; }

    [JsonPropertyName("disk")]
    public int Disk { get; set; }

    [JsonPropertyName("vcpu_count")]
    public int VcpuCount { get; set; }

    [JsonPropertyName("date_created")]
    public string DateCreated { get; set; } = string.Empty;

    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? Id : Label;
    public string StatusBadge => Status.ToUpperInvariant();
}

public class VultrInstanceListResponse
{
    [JsonPropertyName("instances")]
    public List<VultrInstance> Instances { get; set; } = new();
}
