using Forge.Models;
using Forge.Services.PackageManager;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

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

                    bool isProtocolUrl = app.InstallUrl.Contains("://") &&
                        !app.InstallUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase);

                    if (isProtocolUrl)
                    {
                        app.Status = AppStatus.Installed;
                        app.IsInstalled = true;

                        string defaultResult = DefaultAppService.ApplyDefaults(app.PostInstallAction);

                        if (!string.IsNullOrEmpty(defaultResult))
                        {
                            DefaultApplied?.Invoke(this, $"{app.Name}: {defaultResult}");
                        }
                    }
                    else
                    {
                        OutputLine?.Invoke(this,
                            $"Opening download page for {app.Name}. Install it manually from the website.");
                        app.Status = AppStatus.Available;
                    }

                    continue;
                }

                if (!string.IsNullOrWhiteSpace(app.DirectUrl))
                {
                    OutputLine?.Invoke(this,
                        $"Downloading and installing {app.Name}...");

                    await DownloadAndRunAsync(app.DirectUrl);

                    app.Status = AppStatus.Installed;
                    app.IsInstalled = true;

                    string directDefaultResult = DefaultAppService.ApplyDefaults(app.PostInstallAction);

                    if (!string.IsNullOrEmpty(directDefaultResult))
                    {
                        DefaultApplied?.Invoke(this, $"{app.Name}: {directDefaultResult}");
                    }

                    continue;
                }

                string? bundledInstaller = FindBundledInstaller(app);

                if (bundledInstaller is not null)
                {
                    OutputLine?.Invoke(this,
                        $"Using bundled installer for {app.Name}...");

                    await RunBundledInstallerAsync(bundledInstaller);

                    app.Status = AppStatus.Installed;
                    app.IsInstalled = true;

                    string bundledDefaultResult = DefaultAppService.ApplyDefaults(app.PostInstallAction);

                    if (!string.IsNullOrEmpty(bundledDefaultResult))
                    {
                        DefaultApplied?.Invoke(this, $"{app.Name}: {bundledDefaultResult}");
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

    private static readonly Dictionary<string, string> BundledInstallers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NVIDIA App"] = "NVIDIA_app_v11.0.8.299.exe"
    };

    private static string? FindBundledInstaller(AppItem app)
    {
        if (!BundledInstallers.TryGetValue(app.Name, out string? fileName))
        {
            return null;
        }

        string appDir = AppDomain.CurrentDomain.BaseDirectory;

        string[] searchPaths =
        [
            Path.Combine(appDir, "Resources", "Installers", fileName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", fileName)
        ];

        foreach (string path in searchPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static async Task RunBundledInstallerAsync(string installerPath)
    {
        bool isMsi = installerPath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase);

        using var process = new Process();

        process.StartInfo = isMsi
            ? new ProcessStartInfo
            {
                FileName = "msiexec.exe",
                Arguments = $"/i \"{installerPath}\" /qn /norestart",
                UseShellExecute = false
            }
            : new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/silent /install",
                UseShellExecute = false
            };

        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Bundled installer '{Path.GetFileName(installerPath)}' exited with code {process.ExitCode}.");
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

    private static readonly HttpClient Http = CreateHttpClient();

    private static async Task DownloadAndRunAsync(string url)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Forge", "Installers");

        Directory.CreateDirectory(tempDir);

        string fileName = url.Split('?')[0].Split('/').Last();

        string localPath = Path.Combine(tempDir, fileName);

        using (var download = await Http.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead))
        {
            download.EnsureSuccessStatusCode();

            using var sourceStream = await download.Content.ReadAsStreamAsync();
            using var fileStream = File.Create(localPath);

            await sourceStream.CopyToAsync(fileStream);
        }

        bool isMsi = fileName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase);

        using var process = new Process();

        process.StartInfo = isMsi
            ? new ProcessStartInfo
            {
                FileName = "msiexec.exe",
                Arguments = $"/i \"{localPath}\" /qn /norestart",
                UseShellExecute = false
            }
            : new ProcessStartInfo
            {
                FileName = localPath,
                Arguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART /SP-",
                UseShellExecute = false
            };

        process.Start();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Installer '{fileName}' exited with code {process.ExitCode}.");
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();

        client.Timeout = TimeSpan.FromMinutes(20);

        client.DefaultRequestHeaders.UserAgent.ParseAdd("Forge-App-Manager");

        return client;
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