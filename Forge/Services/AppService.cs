using Forge.Models;
using System.IO;
using System.Text.Json;

namespace Forge.Services;

public static class AppService
{
    public static List<AppItem> LoadApps()
    {
        string path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Config",
            "apps.json");

        if (!File.Exists(path))
            return [];

        string json = File.ReadAllText(path);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        return JsonSerializer.Deserialize<List<AppItem>>(json, options)
               ?.OrderByDescending(app => app.Featured)
               .ThenByDescending(app => app.Recommended)
               .ThenBy(app => app.Category)
               .ThenBy(app => app.Name)
               .ToList()
               ?? [];
    }
}