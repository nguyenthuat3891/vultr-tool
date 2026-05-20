using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using ToolVPS.Models;

namespace ToolVPS.Services;

public class VultrService
{
    private readonly HttpClient _http;
    private const string BaseUrl = "https://api.vultr.com/v2/";

    public VultrService(HttpClient http)
    {
        _http = http;
        _http.BaseAddress = new Uri(BaseUrl);
    }

    public void SetApiKey(string apiKey)
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<List<VultrInstance>> GetInstancesAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("instances", ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<VultrInstanceListResponse>(json);
        return result?.Instances ?? new List<VultrInstance>();
    }

    public async Task<VultrInstance?> GetInstanceAsync(string id, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"instances/{id}", ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("instance", out var instanceEl))
            return JsonSerializer.Deserialize<VultrInstance>(instanceEl.GetRawText());

        return null;
    }

    public async Task<bool> RebootInstanceAsync(string id, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"instances/{id}/reboot", null, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> HaltInstanceAsync(string id, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"instances/{id}/halt", null, ct);
        return response.IsSuccessStatusCode;
    }
}
