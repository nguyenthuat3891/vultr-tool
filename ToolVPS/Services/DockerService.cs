using System.Text.Json;
using ToolVPS.Models;

namespace ToolVPS.Services;

public class DockerService
{
    private readonly SshService _ssh;

    public DockerService(SshService ssh)
    {
        _ssh = ssh;
    }

    public bool CheckDocker()
    {
        try
        {
            var result = _ssh.ExecuteCommand("docker --version 2>&1");
            return result.Contains("Docker version", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public string GetDockerVersion()
    {
        try { return _ssh.ExecuteCommand("docker --version 2>&1").Trim(); }
        catch { return "Unknown"; }
    }

    public string GetDockerInfo()
    {
        try { return _ssh.ExecuteCommand("docker info --format 'Containers: {{.Containers}}  Running: {{.ContainersRunning}}  Images: {{.Images}}' 2>&1").Trim(); }
        catch { return string.Empty; }
    }

    public List<DockerContainer> GetContainers()
    {
        var result = new List<DockerContainer>();
        try
        {
            // Each line is a JSON object
            var raw = _ssh.ExecuteCommand(
                "docker ps -a --format '{{json .}}' 2>&1");

            foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("{")) continue;
                try
                {
                    var c = JsonSerializer.Deserialize<DockerContainer>(trimmed);
                    if (c != null) result.Add(c);
                }
                catch { }
            }
        }
        catch { }
        return result;
    }

    public List<DockerComposeService> GetComposeServices()
    {
        var result = new List<DockerComposeService>();
        try
        {
            // Try docker compose (v2) first, fall back to docker-compose (v1)
            var raw = _ssh.ExecuteCommand(
                "docker compose ps --format json 2>/dev/null || docker-compose ps --format json 2>&1");

            // Response is either a JSON array or one JSON object per line
            raw = raw.Trim();
            if (raw.StartsWith("["))
            {
                var list = JsonSerializer.Deserialize<List<DockerComposeService>>(raw);
                if (list != null) result.AddRange(list);
            }
            else
            {
                foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = line.Trim();
                    if (!trimmed.StartsWith("{")) continue;
                    try
                    {
                        var s = JsonSerializer.Deserialize<DockerComposeService>(trimmed);
                        if (s != null) result.Add(s);
                    }
                    catch { }
                }
            }
        }
        catch { }
        return result;
    }

    public string GetContainerLogs(string containerId, int lines = 100)
    {
        try
        {
            return _ssh.ExecuteCommand($"docker logs --tail {lines} {containerId} 2>&1");
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    public string StartContainer(string containerId)
    {
        try { return _ssh.ExecuteCommand($"docker start {containerId} 2>&1").Trim(); }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    public string StopContainer(string containerId)
    {
        try { return _ssh.ExecuteCommand($"docker stop {containerId} 2>&1").Trim(); }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    public string RestartContainer(string containerId)
    {
        try { return _ssh.ExecuteCommand($"docker restart {containerId} 2>&1").Trim(); }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    public string PullImage(string image)
    {
        try { return _ssh.ExecuteCommand($"docker pull {image} 2>&1"); }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    public string ComposeUp(string directory)
    {
        try { return _ssh.ExecuteCommand($"cd {directory} && docker compose up -d 2>&1"); }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    public string ComposeDown(string directory)
    {
        try { return _ssh.ExecuteCommand($"cd {directory} && docker compose down 2>&1"); }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }
}
