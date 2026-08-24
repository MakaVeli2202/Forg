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
        var columns = System.Text.RegularExpressions.Regex
            .Split(line.Trim(), @"\s{2,}");

        if (columns.Length < 2)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(app.WingetId) &&
            columns.Any(column =>
                column.Equals(app.WingetId, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(app.Name) &&
            columns[0].Equals(app.Name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
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