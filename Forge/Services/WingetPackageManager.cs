using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;

namespace Forge.Services.PackageManager;

public class WingetPackageManager : IPackageManager
{
    private static readonly HttpClient Http = CreateHttpClient();

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
        if (IsGitHubSource(source))
        {
            await InstallFromGitHubAsync(packageId);
            return;
        }

        await RunAsync(
            $"install --id {packageId} --exact --silent --accept-package-agreements --accept-source-agreements{SourceArg(source)}");
    }

    public async Task UninstallAsync(string packageId, string? source = null)
    {
        if (IsGitHubSource(source))
        {
            await RunAsync(
                $"uninstall --name \"{packageId}\" --silent --disable-interactivity");
            return;
        }

        await RunAsync(
            $"uninstall --id {packageId} --exact{SourceArg(source)}");
    }

    public async Task UpgradeAsync(string packageId, string? source = null)
    {
        if (IsGitHubSource(source))
        {
            await InstallFromGitHubAsync(packageId);
            return;
        }

        await RunAsync(
            $"upgrade --id {packageId} --exact{SourceArg(source)}");
    }

    private static bool IsGitHubSource(string? source) =>
        string.Equals(source, "github", StringComparison.OrdinalIgnoreCase);

    private async Task InstallFromGitHubAsync(string repoSlug)
    {
        string assetUrl = await ResolveLatestAssetAsync(repoSlug);

        string tempDir = Path.Combine(
            Path.GetTempPath(),
            "Forge",
            "Installers");

        Directory.CreateDirectory(tempDir);

        string fileName = assetUrl.Split('/').Last();
        string localPath = Path.Combine(tempDir, fileName);

        using (var download = await Http.GetAsync(
            assetUrl,
            HttpCompletionOption.ResponseHeadersRead))
        {
            download.EnsureSuccessStatusCode();

            using var sourceStream = await download.Content.ReadAsStreamAsync();
            using var fileStream = File.Create(localPath);

            await sourceStream.CopyToAsync(fileStream);
        }

        try
        {
            bool isMsi = fileName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase);

            _currentProcess = new Process();

            _currentProcess.StartInfo = isMsi
                ? new ProcessStartInfo
                {
                    FileName = "msiexec.exe",
                    Arguments = $"/i \"{localPath}\" /qn /norestart",
                    UseShellExecute = false
                }
                : new ProcessStartInfo
                {
                    FileName = localPath,
                    Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-",
                    UseShellExecute = false
                };

            _currentProcess.Start();

            await _currentProcess.WaitForExitAsync();

            if (_currentProcess.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Installer '{fileName}' exited with code {_currentProcess.ExitCode}.");
            }
        }
        finally
        {
            _currentProcess = null;

            try
            {
                File.Delete(localPath);
            }
            catch
            {
            }
        }
    }

    private static async Task<string> ResolveLatestAssetAsync(string repoSlug)
    {
        using var response = await Http.GetAsync(
            $"https://api.github.com/repos/{repoSlug}/releases/latest");

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                $"'{repoSlug}' has no published releases yet.");
        }

        response.EnsureSuccessStatusCode();

        var release = await response.Content.ReadFromJsonAsync<GitHubRelease>();

        var assets = release?.Assets ?? [];

        if (assets.Count == 0)
        {
            throw new InvalidOperationException(
                $"Latest release of '{repoSlug}' contains no files to install.");
        }

        var installer =
            assets.FirstOrDefault(a => a.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)) ??
            assets.FirstOrDefault(a =>
                a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                a.Name.Contains("setup", StringComparison.OrdinalIgnoreCase)) ??
            assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

        if (installer == null)
        {
            throw new InvalidOperationException(
                $"Latest release of '{repoSlug}' has no .exe or .msi installer.");
        }

        return installer.BrowserDownloadUrl;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();

        client.DefaultRequestHeaders.UserAgent.ParseAdd("Forge-App-Manager");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        return client;
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

    private sealed class GitHubRelease
    {
        [System.Text.Json.Serialization.JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        public List<GitHubAsset> Assets { get; set; } = [];
    }

    private sealed class GitHubAsset
    {
        public string Name { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}
