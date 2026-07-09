using Forge.Models;
using System.Diagnostics;

namespace Forge.Services;

public class DetectionService
{
    public async Task DetectInstalledAppsAsync(
        List<AppItem> apps)
    {
        var output = await GetWingetListAsync();
        var lines = output
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimStart())
            .ToList();

        foreach (var app in apps)
        {
            app.IsInstalled = lines.Any(line =>
                LineMatchesApp(line, app));
        }
    }

    private static bool LineMatchesApp(
        string line,
        AppItem app)
    {
        if (line.Contains(app.WingetId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(app.Name))
        {
            return false;
        }

        return line.StartsWith(app.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> GetWingetListAsync()
    {
        using var _currentProcess = new Process();

        _currentProcess.StartInfo = new ProcessStartInfo
        {
            FileName = "winget",
            Arguments = "list",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _currentProcess.Start();

        string output =
            await _currentProcess.StandardOutput.ReadToEndAsync();

        await _currentProcess.WaitForExitAsync();

        return output;
    }
}