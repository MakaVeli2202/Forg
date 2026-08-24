using Forge.Models;
using Forge.Services.PackageManager;

namespace Forge.Services;

public class InstallService
{
    private readonly IPackageManager _packageManager;

    public event EventHandler<InstallProgressEventArgs>? ProgressChanged;

    public event EventHandler<string>? OutputLine;

    public InstallService()
    {
        _packageManager = new WingetPackageManager();

        if (_packageManager is WingetPackageManager winget)
        {
            winget.OutputLine += (_, line) =>
                OutputLine?.Invoke(this, line);
        }
    }

    public async Task InstallAppsAsync(
        IEnumerable<AppItem> apps)
    {
        var appList = apps.ToList();

        int total = appList.Count;
        int current = 0;

        foreach (var app in appList)
        {
            current++;

            try
            {
                app.Status = AppStatus.Installing;

                ProgressChanged?.Invoke(
                    this,
                    new InstallProgressEventArgs(
                        current,
                        total,
                        app.Name));

                await _packageManager.InstallAsync(
                    app.IsGitHubSource ? app.GitHubRepo! : app.WingetId,
                    app.Source);

                app.Status = AppStatus.Installed;
                app.IsInstalled = true;
            }
            catch
            {
                app.Status = AppStatus.Failed;
            }
        }
    }

    public async Task UpgradeAppsAsync(
        IEnumerable<AppItem> apps)
    {
        var appList = apps.ToList();

        int total = appList.Count;
        int current = 0;

        foreach (var app in appList)
        {
            current++;

            try
            {
                app.Status = AppStatus.Upgrading;

                ProgressChanged?.Invoke(
                    this,
                    new InstallProgressEventArgs(
                        current,
                        total,
                        app.Name));

                await _packageManager.UpgradeAsync(
                    app.IsGitHubSource ? app.GitHubRepo! : app.WingetId,
                    app.Source);

                app.Status = AppStatus.Installed;
            }
            catch
            {
                app.Status = AppStatus.Failed;
            }
        }
    }

    public async Task UninstallAppsAsync(
        IEnumerable<AppItem> apps)
    {
        var appList = apps.ToList();

        int total = appList.Count;
        int current = 0;

        foreach (var app in appList)
        {
            current++;

            try
            {
                app.Status = AppStatus.Uninstalling;

                ProgressChanged?.Invoke(
                    this,
                    new InstallProgressEventArgs(
                        current,
                        total,
                        app.Name));

                await _packageManager.UninstallAsync(
                    app.IsGitHubSource ? app.Name : app.WingetId,
                    app.Source);

                app.IsInstalled = false;
                app.IsSelected = false;

                app.Status = AppStatus.Available;
            }
            catch
            {
                app.Status = AppStatus.Failed;
            }
        }
    }

    public void Cancel()
    {
        if (_packageManager is WingetPackageManager winget)
        {
            winget.Cancel();
        }
    }
}

public class InstallProgressEventArgs : EventArgs
{
    public int Current { get; }

    public int Total { get; }

    public string AppName { get; }

    public InstallProgressEventArgs(
        int current,
        int total,
        string appName)
    {
        Current = current;
        Total = total;
        AppName = appName;
    }
}