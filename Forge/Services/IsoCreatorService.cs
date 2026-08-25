using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Forge.Services;

public sealed class IsoEditionInfo
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Size { get; set; } = "";
}

public sealed class IsoCreatorResult
{
    public bool Success { get; set; }
    public string OutputPath { get; set; } = "";
    public string Message { get; set; } = "";
    public long SizeBytes { get; set; }
}

public sealed class IsoCreatorService
{
    private static readonly TimeSpan DismTimeout = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MountTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CopyTimeout = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan RepackTimeout = TimeSpan.FromMinutes(30);

    private readonly Action<string>? _log;
    private string? _dismPath;
    private string? _oscdimgPath;

    public IsoCreatorService(Action<string>? log = null)
    {
        _log = log;
    }

    public bool HasDism => !string.IsNullOrEmpty(_dismPath);
    public bool HasOscdimg => !string.IsNullOrEmpty(_oscdimgPath);
    public string? DismPath => _dismPath;
    public string? OscdimgPath => _oscdimgPath;

    private void Log(string msg) => _log?.Invoke(msg);

    public void DetectTools()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var dism = Path.Combine(winDir, "System32", "dism.exe");
        _dismPath = File.Exists(dism) ? dism : null;

        _oscdimgPath = ResolveOscdimg();
        Log($"DISM: {_dismPath ?? "NOT FOUND"}");
        Log($"oscdimg: {_oscdimgPath ?? "NOT FOUND"}");
    }

    private static string? ResolveOscdimg()
    {
        var baseDir = AppContext.BaseDirectory;
        var bundled = Path.Combine(baseDir, "tools", "oscdimg", "oscdimg.exe");
        if (File.Exists(bundled)) return bundled;

        foreach (var pf in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                 })
        {
            if (string.IsNullOrEmpty(pf)) continue;
            var deployRoot = Path.Combine(pf,
                "Windows Kits", "10", "Assessment and Deployment Kit", "Deployment Tools");
            if (!Directory.Exists(deployRoot)) continue;

            try
            {
                var matches = Directory
                    .EnumerateFiles(deployRoot, "oscdimg.exe", SearchOption.AllDirectories)
                    .OrderByDescending(p => p.Contains("amd64", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (matches.Count > 0) return matches[0];
            }
            catch
            {
            }
        }
        return null;
    }

    public static bool ValidateMedia(string mediaDir, out string reason)
    {
        var sources = Path.Combine(mediaDir, "sources");
        var hasInstall = File.Exists(Path.Combine(sources, "install.wim"))
                         || File.Exists(Path.Combine(sources, "install.esd"));
        var hasBoot = File.Exists(Path.Combine(sources, "boot.wim"));

        reason = (hasInstall, hasBoot) switch
        {
            (false, _) => "Missing sources\\install.wim or install.esd.",
            (_, false) => "Missing sources\\boot.wim.",
            _ => string.Empty,
        };
        return string.IsNullOrEmpty(reason);
    }

    public static string? FindInstallImage(string rootDir)
    {
        var wim = Path.Combine(rootDir, "sources", "install.wim");
        if (File.Exists(wim)) return wim;
        var esd = Path.Combine(rootDir, "sources", "install.esd");
        return File.Exists(esd) ? esd : null;
    }

    public async Task<List<IsoEditionInfo>> GetEditionsAsync(string isoPath, CancellationToken ct = default)
    {
        if (_dismPath == null) throw new InvalidOperationException("DISM not found.");

        string? driveLetter = null;
        try
        {
            driveLetter = await MountIsoAsync(isoPath, ct);
            var installImage = FindInstallImage($"{driveLetter}:\\");
            if (installImage == null)
                throw new FileNotFoundException("No install.wim or install.esd found in ISO.");

            var args = $"/English /Get-ImageInfo /ImageFile:\"{installImage}\"";
            var result = await RunProcessAsync(_dismPath, args, ct);
            return ParseImageInfo(result.StdOut);
        }
        finally
        {
            if (driveLetter != null)
                await DismountIsoAsync(isoPath);
        }
    }

    public async Task<IsoCreatorResult> BuildIsoAsync(
        string isoPath,
        string outputDir,
        string outputName,
        int editionIndex,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        if (_dismPath == null) throw new InvalidOperationException("DISM not found.");
        if (_oscdimgPath == null) throw new InvalidOperationException("oscdimg not found.");

        var workspaceDir = Path.Combine(Path.GetTempPath(), "Forge", "IsoBuild", Guid.NewGuid().ToString("N"));
        var mediaDir = Path.Combine(workspaceDir, "media");
        var mountDir = Path.Combine(workspaceDir, "mount");
        var sourcesDir = Path.Combine(mediaDir, "sources");
        var mounted = false;

        try
        {
            onProgress?.Invoke("Preparing workspace...");
            Directory.CreateDirectory(mediaDir);
            Directory.CreateDirectory(mountDir);

            onProgress?.Invoke("Mounting ISO...");
            Log("Mounting source ISO...");
            var driveLetter = await MountIsoAsync(isoPath, ct);
            try
            {
                onProgress?.Invoke("Extracting ISO contents...");
                Log("Extracting ISO via robocopy...");
                await RobocopyAsync($"{driveLetter}:\\", mediaDir, ct);
            }
            finally
            {
                onProgress?.Invoke("Dismounting source ISO...");
                await DismountIsoAsync(isoPath);
            }

            if (!ValidateMedia(mediaDir, out var reason))
                throw new InvalidOperationException($"Invalid Windows media: {reason}");

            onProgress?.Invoke("Preparing install image...");
            Log("Ensuring editable WIM format...");
            var (wimPath, index) = await EnsureEditableWimAsync(sourcesDir, editionIndex, ct);

            onProgress?.Invoke("Committing image changes...");
            Log($"Mounting image index {index}...");
            await MountWimAsync(wimPath, index, mountDir, ct);
            mounted = true;

            Log("Unmounting image (commit)...");
            await UnmountWimAsync(mountDir, commit: true, ct);
            mounted = false;

            var outputIsoPath = Path.Combine(outputDir, $"{outputName}.iso");
            Directory.CreateDirectory(outputDir);

            onProgress?.Invoke("Creating bootable ISO...");
            Log($"Repacking ISO → {outputIsoPath}");
            await RepackIsoAsync(mediaDir, outputIsoPath, ct);

            var sizeBytes = new FileInfo(outputIsoPath).Length;
            Log($"ISO created: {outputIsoPath} ({sizeBytes / 1024 / 1024} MB)");

            return new IsoCreatorResult
            {
                Success = true,
                OutputPath = outputIsoPath,
                Message = $"ISO created: {outputIsoPath} ({sizeBytes / 1024 / 1024} MB)",
                SizeBytes = sizeBytes,
            };
        }
        catch (Exception ex)
        {
            if (mounted)
            {
                Log("Failure mid-mount — discarding WIM mount...");
                try { await UnmountWimAsync(mountDir, commit: false, CancellationToken.None); }
                catch { }
            }
            Log($"Build failed: {ex.Message}");
            return new IsoCreatorResult
            {
                Success = false,
                Message = ex.Message,
            };
        }
        finally
        {
            CleanupDirectory(workspaceDir);
        }
    }

    private async Task<string> MountIsoAsync(string isoPath, CancellationToken ct)
    {
        var ps = isoPath.Replace("'", "''");
        var script =
            $"Mount-DiskImage -ImagePath '{ps}' | Out-Null; " +
            "for ($i=0; $i -lt 20; $i++) { " +
            $"  $v = (Get-DiskImage -ImagePath '{ps}' | Get-Volume).DriveLetter; " +
            "  if ($v) { $v; break }; Start-Sleep -Milliseconds 250 }";

        var result = await RunProcessAsync("powershell.exe",
            $"-NoProfile -NonInteractive -Command \"{script}\"", ct, MountTimeout);

        var letter = result.StdOut.Replace("\r", "").Split('\n')
            .Select(l => l.Trim())
            .LastOrDefault(l => l.Length == 1 && char.IsLetter(l[0]));

        if (string.IsNullOrEmpty(letter))
            throw new InvalidOperationException($"Could not determine ISO drive letter. {result.StdErr.Trim()}");
        return letter;
    }

    private async Task DismountIsoAsync(string isoPath)
    {
        var ps = isoPath.Replace("'", "''");
        var script = $"Dismount-DiskImage -ImagePath '{ps}'";
        await RunProcessAsync("powershell.exe",
            $"-NoProfile -NonInteractive -Command \"{script}\"", CancellationToken.None, MountTimeout);
    }

    private async Task RobocopyAsync(string source, string dest, CancellationToken ct)
    {
        var args = $"\"{source.TrimEnd('\\')}\" \"{dest}\" /E /A-:R /R:5 /W:2 /NFL /NDL /NJH /NJS /NP";
        var result = await RunProcessAsync("robocopy.exe", args, ct, CopyTimeout);
        if (result.ExitCode >= 8)
            throw new InvalidOperationException($"robocopy failed (exit {result.ExitCode}). {result.StdErr.Trim()}");
    }

    private async Task<(string WimPath, int Index)> EnsureEditableWimAsync(
        string sourcesDir, int index, CancellationToken ct)
    {
        var wim = Path.Combine(sourcesDir, "install.wim");
        if (File.Exists(wim)) return (wim, index);

        var esd = Path.Combine(sourcesDir, "install.esd");
        if (!File.Exists(esd))
            throw new FileNotFoundException("Neither install.wim nor install.esd found.", sourcesDir);

        Log($"Exporting ESD index {index} → WIM (max compression)...");
        var args = $"/English /Export-Image /SourceImageFile:\"{esd}\" /SourceIndex:{index} " +
                   $"/DestinationImageFile:\"{wim}\" /Compress:max /CheckIntegrity";
        var result = await RunProcessAsync(_dismPath!, args, ct, DismTimeout);
        EnsureSuccess(result, "Export-Image (ESD→WIM)");

        try { File.Delete(esd); } catch { }
        return (wim, 1);
    }

    private async Task MountWimAsync(string wimPath, int index, string mountDir, CancellationToken ct)
    {
        Directory.CreateDirectory(mountDir);
        ClearReadOnly(wimPath);
        Log($"Mounting image index {index} → {mountDir}");
        var args = $"/English /Mount-Wim /WimFile:\"{wimPath}\" /Index:{index} /MountDir:\"{mountDir}\"";
        var result = await RunProcessAsync(_dismPath!, args, ct, DismTimeout);
        EnsureSuccess(result, "Mount-Wim");
    }

    private async Task UnmountWimAsync(string mountDir, bool commit, CancellationToken ct)
    {
        var mode = commit ? "/Commit" : "/Discard";
        Log($"Unmounting {mountDir} ({mode})");
        var args = $"/English /Unmount-Wim /MountDir:\"{mountDir}\" {mode}";
        var result = await RunProcessAsync(_dismPath!, args, ct, DismTimeout);
        if (!result.Success)
        {
            Log($"Unmount-Wim failed ({result.ExitCode})");
            if (commit) EnsureSuccess(result, "Unmount-Wim (/Commit)");
        }
    }

    private async Task RepackIsoAsync(string mediaDir, string outIsoPath, CancellationToken ct)
    {
        var biosBoot = Path.Combine(mediaDir, "boot", "etfsboot.com");
        var uefiBoot = Path.Combine(mediaDir, "efi", "microsoft", "boot", "efisys.bin");
        if (!File.Exists(biosBoot)) throw new FileNotFoundException("BIOS boot image missing.", biosBoot);
        if (!File.Exists(uefiBoot)) throw new FileNotFoundException("UEFI boot image missing.", uefiBoot);

        EnsureBootChainIntact(mediaDir);

        var bootData = $"2#p0,e,b\"{biosBoot}\"#pEF,e,b\"{uefiBoot}\"";
        var args = $"-bootdata:{bootData} -u2 -udfver102 -m -o -lFORGE " +
                   $"\"{mediaDir}\" \"{outIsoPath}\"";

        var result = await RunProcessAsync(_oscdimgPath!, args, ct, RepackTimeout);
        if (!result.Success)
            throw new InvalidOperationException($"oscdimg failed (exit {result.ExitCode}). {result.StdErr.Trim()}");
    }

    private static void EnsureBootChainIntact(string mediaDir)
    {
        var required = new[]
        {
            Path.Combine(mediaDir, "bootmgr"),
            Path.Combine(mediaDir, "bootmgr.efi"),
            Path.Combine(mediaDir, "boot", "bcd"),
            Path.Combine(mediaDir, "efi", "microsoft", "boot", "bcd"),
        };
        var missing = required.Where(p => !File.Exists(p)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Boot chain incomplete — missing: {string.Join(", ", missing)}");
    }

    private async Task<ProcessResult> RunProcessAsync(
        string fileName, string arguments, CancellationToken ct,
        TimeSpan? timeout = null)
    {
        Log($"RUN: {fileName} {arguments}");

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) stderr.AppendLine(e.Data);
        };

        if (!process.Start())
            throw new InvalidOperationException($"Failed to start: {fileName}");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (timeout is { } t) linkedCts.CancelAfter(t);

        try
        {
            await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(true); } catch { }
            throw;
        }

        var result = new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
        Log($"EXIT {result.ExitCode}: {fileName}");
        return result;
    }

    private static List<IsoEditionInfo> ParseImageInfo(string output)
    {
        var editions = new List<IsoEditionInfo>();
        var blocks = Regex.Split(output, @"(?=Index\s*:\s*\d)", RegexOptions.Multiline);

        foreach (var block in blocks)
        {
            var indexMatch = Regex.Match(block, @"Index\s*:\s*(\d+)");
            var nameMatch = Regex.Match(block, @"Name\s*:\s*(.+)");
            var descMatch = Regex.Match(block, @"Description\s*:\s*(.+)");
            var sizeMatch = Regex.Match(block, @"Size\s*:\s*(\d[\d,\.]*\s*\w+)");

            if (indexMatch.Success)
            {
                editions.Add(new IsoEditionInfo
                {
                    Index = int.Parse(indexMatch.Groups[1].Value),
                    Name = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : $"Edition {indexMatch.Groups[1].Value}",
                    Description = descMatch.Success ? descMatch.Groups[1].Value.Trim() : "",
                    Size = sizeMatch.Success ? sizeMatch.Groups[1].Value.Trim() : "",
                });
            }
        }
        return editions;
    }

    private static void EnsureSuccess(ProcessResult r, string op)
    {
        if (!r.Success)
        {
            var text = string.IsNullOrWhiteSpace(r.StdErr) ? r.StdOut : r.StdErr;
            var firstError = text.Replace("\r\n", "\n").Split('\n')
                .FirstOrDefault(l => l.Contains("Error")) ?? text.Trim();
            throw new InvalidOperationException($"DISM {op} failed (exit {r.ExitCode}). {firstError}");
        }
    }

    private static void ClearReadOnly(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            var attrs = File.GetAttributes(path);
            if (attrs.HasFlag(FileAttributes.ReadOnly))
                File.SetAttributes(path, attrs & ~FileAttributes.ReadOnly);
        }
        catch { }
    }

    private static void CleanupDirectory(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return;
            foreach (var entry in Directory.EnumerateFileSystemEntries(dir, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var attrs = File.GetAttributes(entry);
                    if (attrs.HasFlag(FileAttributes.ReadOnly))
                        File.SetAttributes(entry, attrs & ~FileAttributes.ReadOnly);
                }
                catch { }
            }
            for (var attempt = 1; attempt <= 5; attempt++)
            {
                try { Directory.Delete(dir, true); return; }
                catch when (attempt < 5 && Directory.Exists(dir))
                {
                    Thread.Sleep(attempt * 500);
                }
            }
        }
        catch { }
    }

    private sealed record ProcessResult(int ExitCode, string StdOut, string StdErr)
    {
        public bool Success => ExitCode == 0;
    }
}
