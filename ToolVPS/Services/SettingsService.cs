using System.IO;
using System.Text.Json;
using ToolVPS.Models;

namespace ToolVPS.Services;

public class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ToolVPS", "settings.json");

    private AppSettings _settings = new();

    public AppSettings Settings => _settings;

    public void Load()
    {
        if (!File.Exists(SettingsPath))
            return;

        try
        {
            var json = File.ReadAllText(SettingsPath);
            _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            _settings = new AppSettings();
        }
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
