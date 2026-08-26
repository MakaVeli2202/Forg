using Forge.Models;
using Forge.Services.PackageManager;
using System.Diagnostics;

namespace Forge.Services;

public class InstallService
{
    private readonly IPackageManager _packageManager;

    public event EventHandler<InstallProgressEventArgs>? ProgressChanged;

    public event EventHandler<string>? OutputLine;

    public event EventHandler<string>? DefaultApplied;

    public event EventHandler<string>? PreInstallMessage;

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

                if (!string.IsNullOrWhiteSpace(app.PreInstallMessage))
                {
                    PreInstallMessage?.Invoke(this, app.PreInstallMessage);
                }

                if (!string.IsNullOrWhiteSpace(app.DependsOn))
                {
                    await InstallDependencyIfNeeded(app);
                }

                if (!string.IsNullOrWhiteSpace(app.InstallUrl))
                {
                    OpenExternalInstaller(app);
                    app.Status = AppStatus.Installed;
                    app.IsInstalled = true;

                    string defaultResult = DefaultAppService.ApplyDefaults(app.PostInstallAction);

                    if (!string.IsNullOrEmpty(defaultResult))
                    {
                        DefaultApplied?.Invoke(this, $"{app.Name}: {defaultResult}");
                    }

                    continue;
                }

                await _packageManager.InstallAsync(
                    app.IsGitHubSource ? app.GitHubRepo! : app.WingetId,
                    app.Source);

                app.Status = AppStatus.Installed;
                app.IsInstalled = true;

                string installDefaultResult = DefaultAppService.ApplyDefaults(app.PostInstallAction);

                if (!string.IsNullOrEmpty(installDefaultResult))
                {
                    DefaultApplied?.Invoke(this, $"{app.Name}: {installDefaultResult}");
                }
            }
            catch
            {
                app.Status = AppStatus.Failed;
            }
        }
    }

    private async Task InstallDependencyIfNeeded(AppItem app)
    {
        var allApps = AppService.LoadApps();
        var dep = allApps.FirstOrDefault(a =>
            string.Equals(a.WingetId, app.DependsOn, StringComparison.OrdinalIgnoreCase));

        if (dep is null)
        {
            return;
        }

        if (dep.IsInstalled)
        {
            return;
        }

        OutputLine?.Invoke(this,
            $"Installing dependency: {dep.Name} for {app.Name}...");

        dep.Status = AppStatus.Installing;

        await _packageManager.InstallAsync(
            dep.WingetId,
            dep.Source);

        dep.Status = AppStatus.Installed;
        dep.IsInstalled = true;

        OutputLine?.Invoke(this,
            $"Dependency {dep.Name} installed successfully.");
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

                if (!string.IsNullOrWhiteSpace(app.InstallUrl))
                {
                    OpenExternalInstaller(app);
                    app.Status = AppStatus.Installed;
                    continue;
                }

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

                if (!string.IsNullOrWhiteSpace(app.InstallUrl))
                {
                    OutputLine?.Invoke(this,
                        $"Uninstall of {app.Name} must be done inside its store client (e.g. Steam).");
                    app.IsSelected = false;
                    continue;
                }

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

    private static void OpenExternalInstaller(AppItem app)
    {
        using var process = new Process();

        process.StartInfo = new ProcessStartInfo
        {
            FileName = app.InstallUrl!,
            UseShellExecute = true
        };

        process.Start();
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