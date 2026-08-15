using System.Diagnostics;

namespace Forge.Services.PackageManager;

public class WingetPackageManager : IPackageManager
{
    private Process? _currentProcess;

    public bool IsInstalling =>
        _currentProcess != null &&
        !_currentProcess.HasExited;

    public void Cancel()
    {
        try
        {
            if (_currentProcess != null &&
                !_currentProcess.HasExited)
            {
                _currentProcess.Kill(true);
            }
        }
        catch
        {
        }
    }

    public async Task InstallAsync(string packageId, string? source = null)
    {
        await RunAsync(
            $"install --id {packageId} --exact --silent --accept-package-agreements --accept-source-agreements{SourceArg(source)}");
    }

    public async Task UninstallAsync(string packageId, string? source = null)
    {
        await RunAsync(
            $"uninstall --id {packageId} --exact{SourceArg(source)}");
    }

    public async Task UpgradeAsync(string packageId, string? source = null)
    {
        await RunAsync(
            $"upgrade --id {packageId} --exact{SourceArg(source)}");
    }

    private static string SourceArg(string? source) =>
        string.IsNullOrWhiteSpace(source)
            ? string.Empty
            : $" --source {source}";

    private async Task RunAsync(string arguments)
    {
        _currentProcess = new Process();

        _currentProcess.StartInfo = new ProcessStartInfo
        {
            FileName = "winget",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _currentProcess.Start();

        await _currentProcess.WaitForExitAsync();
    }
}