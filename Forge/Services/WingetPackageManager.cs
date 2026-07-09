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

    public async Task InstallAsync(string packageId)
    {
        await RunAsync(
            $"install --id {packageId} --exact --silent --accept-package-agreements --accept-source-agreements");
    }

    public async Task UninstallAsync(string packageId)
    {
        await RunAsync(
            $"uninstall --id {packageId} --exact");
    }

    public async Task UpgradeAsync(string packageId)
    {
        await RunAsync(
            $"upgrade --id {packageId} --exact");
    }

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