using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Forge.Models;
using Microsoft.Win32;

namespace Forge.Services;

public class TweakService
{
    private const string RemoveEntryToken = "<RemoveEntry>";
    private const string UltimatePerfGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";

    private static readonly Regex GuidRegex =
        new("[A-Fa-f0-9-]{36}", RegexOptions.Compiled);

    private Process? _currentProcess;
    private List<TweakItem>? _tweaks;

    public event EventHandler<string>? Log;

    private static event Action<string>? RegistryIssue;

    private static TweakService? _logForwardTarget;

    public TweakService()
    {
        _logForwardTarget = this;

        RegistryIssue -= ForwardRegistryIssue;
        RegistryIssue += ForwardRegistryIssue;
    }

    private static void ForwardRegistryIssue(string message) =>
        _logForwardTarget?.Log?.Invoke(_logForwardTarget, message);

    public IReadOnlyList<TweakItem> Tweaks =>
        _tweaks ?? [];

    public List<TweakItem> LoadTweaks()
    {
        if (_tweaks != null)
        {
            return _tweaks;
        }

        string path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Config",
            "tweaks.json");

        if (!File.Exists(path))
        {
            Log?.Invoke(this, $"Tweak catalog not found: {path}");
            _tweaks = [];
            return _tweaks;
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var order = new Dictionary<string, int>
        {
            ["Boost Performance"] = 0,
            ["Better Privacy"] = 1,
            ["Debloat & Cleanup"] = 2,
            ["Customize & Gaming"] = 3,
            ["Network & Connectivity"] = 4,
            ["System & Advanced"] = 5
        };

        _tweaks = JsonSerializer
            .Deserialize<List<TweakItem>>(json: File.ReadAllText(path), options)
            ?.OrderBy(t => order.GetValueOrDefault(t.Group, 9))
            .ThenBy(t => t.Name)
            .ToList()
            ?? [];

        Log?.Invoke(this, $"Loaded {_tweaks.Count} tweaks.");

        return _tweaks;
    }

    public bool GetAppliedState(TweakItem tweak)
    {
        if (tweak.Registry is { Count: > 0 } &&
            tweak.Registry.All(r => r.Values == null))
        {
            foreach (var entry in tweak.Registry)
            {
                object? current = TryReadValue(entry.Path, entry.Name);

                if (current == null && !string.IsNullOrEmpty(entry.DefaultState))
                {
                    current = entry.DefaultState == "true"
                        ? entry.Value
                        : entry.OriginalValue;
                }

                if (!string.Equals(
                        Normalize(current),
                        Normalize(entry.Value),
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        if (tweak.IsCombobox &&
            tweak.Registry is { Count: > 0 })
        {
            try
            {
                GetComboState(tweak);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        return false;
    }

    public string GetComboState(TweakItem tweak)
    {
        var registry = tweak.Registry!;

        foreach (var state in registry[0].Values!)
        {
            bool matches = true;

            foreach (var setting in registry)
            {
                object? raw = TryReadValue(setting.Path, setting.Name);

                string actual = raw != null
                    ? Normalize(raw)
                    : Normalize(setting.DefaultValue);

                string configured = setting.Values![state.Key];

                string expected = configured == RemoveEntryToken
                    ? Normalize(setting.DefaultValue)
                    : Normalize(configured);

                if (!string.Equals(actual, expected, StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return state.Key;
            }
        }

        throw new InvalidOperationException(
            "Registry values do not match a supported state.");
    }

    public async Task ApplyAsync(TweakItem tweak, CancellationToken ct = default)
    {
        Log?.Invoke(this, $"Applying tweak: {tweak.Name}");

        if (tweak.Services != null)
        {
            foreach (var service in tweak.Services)
            {
                SetService(
                    service,
                    service.StartupType,
                    keepUserStartup: true);
            }
        }

        if (tweak.Registry != null)
        {
            foreach (var entry in tweak.Registry.Where(r => r.Values == null))
            {
                WriteValue(entry.Path, entry.Name, entry.Value, entry.Type);
            }
        }

        if (!string.IsNullOrWhiteSpace(tweak.InvokeScript))
        {
            await RunPowerShellAsync(tweak.InvokeScript!, ct);
        }

        Log?.Invoke(this, $"Tweak applied: {tweak.Name}");
    }

    public async Task UndoAsync(TweakItem tweak, CancellationToken ct = default)
    {
        Log?.Invoke(this, $"Undoing tweak: {tweak.Name}");

        if (tweak.Services != null)
        {
            foreach (var service in tweak.Services)
            {
                SetService(
                    service,
                    service.OriginalType,
                    keepUserStartup: false);
            }
        }

        if (tweak.Registry != null)
        {
            foreach (var entry in tweak.Registry.Where(r => r.Values == null))
            {
                WriteValue(
                    entry.Path,
                    entry.Name,
                    entry.OriginalValue,
                    entry.Type);
            }
        }

        if (!string.IsNullOrWhiteSpace(tweak.UndoScript))
        {
            await RunPowerShellAsync(tweak.UndoScript!, ct);
        }

        Log?.Invoke(this, $"Tweak undone: {tweak.Name}");
    }

    public async Task ApplyComboAsync(TweakItem tweak, string state, CancellationToken ct = default)
    {
        Log?.Invoke(this, $"{tweak.Name}: {state}");

        if (tweak.Id == "ChangeDns")
        {
            await RunDnsScriptAsync(state, ct);
            return;
        }

        if (tweak.Registry == null)
        {
            return;
        }

        foreach (var entry in tweak.Registry)
        {
            if (entry.Values == null ||
                !entry.Values.TryGetValue(state, out var configured))
            {
                continue;
            }

            string value = configured == RemoveEntryToken
                ? entry.DefaultValue ?? string.Empty
                : configured;

            WriteValue(entry.Path, entry.Name, value, entry.Type);
        }
    }

    public async Task RunOosuAsync(CancellationToken ct = default)
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Forge");

        Directory.CreateDirectory(dir);

        string target = Path.Combine(dir, "OOSU10.exe");
        const string url = "https://dl5.oo-software.com/files/ooshutup10/OOSU10.exe";

        using var http = new HttpClient();
        byte[] bytes = await http.GetByteArrayAsync(url, ct);

        await File.WriteAllBytesAsync(target, bytes, ct);

        Log?.Invoke(this, "Launching O&O ShutUp10++.");

        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true
        });
    }

    public bool GetUltimatePerformanceActive()
    {
        try
        {
            string output = RunCaptureAsync("powercfg /getactivescheme", CancellationToken.None)
                .GetAwaiter().GetResult();

            return output.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public async Task SetUltimatePerformanceAsync(bool enable, CancellationToken ct = default)    {
        if (enable)
        {
            string output = await RunCaptureAsync("powercfg /duplicatescheme " + UltimatePerfGuid, ct);

            var match = GuidRegex.Match(output);

            if (!match.Success)
            {
                throw new InvalidOperationException(
                    "Failed to create Ultimate Performance profile.");
            }

            await RunCaptureAsync($"powercfg /setactive {match.Value}", ct);
            Log?.Invoke(this, "Ultimate Performance plan installed and activated.");
        }
        else
        {
            await RunCaptureAsync("powercfg /restoredefaultschemes", ct);
            Log?.Invoke(this, "Power plans reset to defaults.");
        }
    }

    private async Task RunDnsScriptAsync(string provider, CancellationToken ct)
    {
        string dnsJsonPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Config",
            "dns.json");

        string dnsJson = File.Exists(dnsJsonPath)
            ? File.ReadAllText(dnsJsonPath)
            : "{}";

        string script = """
            $DnsConfig = Get-Content -Raw '__DNSJSON__' | ConvertFrom-Json

            function Set-DnsProvider {
                param($Provider)

                if ($Provider -eq 'Default') { return }

                $Adapters = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' }

                if ($Provider -eq 'DHCP') {
                    foreach ($Adapter in $Adapters) {
                        Set-DnsClientServerAddress -InterfaceIndex $Adapter.ifIndex -ResetServerAddresses
                        netsh interface ip set dnsservers name="$($Adapter.Name)" source=dhcp | Out-Null
                        netsh interface ipv6 set dnsservers name="$($Adapter.Name)" source=dhcp | Out-Null

                        $dohInterfaceSettings = "HKLM:\System\CurrentControlSet\Services\Dnscache\InterfaceSpecificParameters\$($Adapter.InterfaceGuid)\DohInterfaceSettings"
                        if (Test-Path $dohInterfaceSettings) {
                            Remove-Item -Path $dohInterfaceSettings -Recurse -Force -ErrorAction SilentlyContinue
                        }
                    }
                    return
                }

                $dns = $DnsConfig.$Provider
                if ($null -eq $dns) { throw "DNS provider '$Provider' was not found." }

                $dohSupported = [bool](Get-Command Add-DnsClientDohServerAddress -ErrorAction SilentlyContinue)

                if ($dns.DohOnly -and -not $dohSupported) {
                    throw "Provider '$Provider' requires DNS over HTTPS which is unsupported here."
                }

                foreach ($Adapter in $Adapters) {
                    $interfaceParams = "HKLM:\System\CurrentControlSet\Services\Dnscache\InterfaceSpecificParameters\$($Adapter.InterfaceGuid)"

                    if ($dohSupported -and $dns.DohTemplate) {
                        try {
                            $ips = @($dns.Primary, $dns.Secondary, $dns.Primary6, $dns.Secondary6) | Where-Object { $_ }
                            foreach ($ip in $ips) {
                                $dohTemplate = if ($dns.SecondaryDohTemplate -and @($dns.Secondary, $dns.Secondary6) -contains $ip) {
                                    $dns.SecondaryDohTemplate
                                } else {
                                    $dns.DohTemplate
                                }
                                $existing = Get-DnsClientDohServerAddress -ServerAddress $ip -ErrorAction SilentlyContinue
                                if ($existing) {
                                    Set-DnsClientDohServerAddress -ServerAddress $ip -DohTemplate $dohTemplate -AllowFallbackToUdp $false -AutoUpgrade $true -ErrorAction Stop
                                } else {
                                    Add-DnsClientDohServerAddress -ServerAddress $ip -DohTemplate $dohTemplate -AllowFallbackToUdp $false -AutoUpgrade $true -ErrorAction Stop
                                }
                                $leaf = if ($ip.Contains(':')) { 'Doh6' } else { 'Doh' }
                                $regPath = "$interfaceParams\DohInterfaceSettings\$leaf\$ip"
                                if (-not (Test-Path $regPath)) {
                                    New-Item -Path $regPath -Force -ErrorAction Stop | Out-Null
                                }
                                New-ItemProperty -Path $regPath -Name 'DohFlags' -Value 1 -PropertyType QWord -Force -ErrorAction Stop | Out-Null
                            }
                        } catch {
                            if ($dns.DohOnly) { throw }
                            Write-Host "DoH setup failed for '$Provider'; continuing with plain DNS."
                        }
                    }

                    $ipv4Addresses = @(@($dns.Primary, $dns.Secondary) | Where-Object { $_ })
                    $ipv6Addresses = @(@($dns.Primary6, $dns.Secondary6) | Where-Object { $_ })

                    Set-DnsClientServerAddress -InterfaceIndex $Adapter.ifIndex -ServerAddresses $ipv4Addresses -ErrorAction Stop
                    Set-DnsClientServerAddress -InterfaceIndex $Adapter.ifIndex -ServerAddresses $ipv6Addresses -ErrorAction Stop
                }

                if ($dohSupported -and $dns.DohTemplate) {
                    Clear-DnsClientCache
                }
            }

            Set-DnsProvider -Provider '__PROVIDER__'
            """;

        script = script
            .Replace("__DNSJSON__", dnsJsonPath.Replace("'", "''"))
            .Replace("__PROVIDER__", provider.Replace("'", "''"));

        await RunPowerShellAsync(script, ct);
    }

    private static void SetService(
        TweakServiceEntry service,
        string startupType,
        bool keepUserStartup)
    {
        using var svc = new System.ServiceProcess.ServiceController(service.Name);

        try
        {
            svc.Refresh();

            if (keepUserStartup &&
                !string.Equals(
                    svc.StartType.ToString(),
                    service.OriginalType,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(
                    svc.StartType.ToString(),
                    startupType,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }
        catch (InvalidOperationException)
        {
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = $"config \"{service.Name}\" start= {ScStartType(startupType)}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi)!;
        process.WaitForExit(30000);
    }

    private static string ScStartType(string startupType) =>
        startupType switch
        {
            "Automatic (Delayed Start)" => "delayed-auto",
            "AutomaticDelayedStart" => "delayed-auto",
            "Automatic" => "auto",
            "Manual" => "demand",
            "Disabled" => "disabled",
            _ => "demand"
        };

    private static void WriteValue(
        string path,
        string name,
        string? value,
        string? type)
    {
        try
        {
            using var key = OpenOrCreateKey(path);

            if (key == null)
            {
                return;
            }

            if (value == RemoveEntryToken)
            {
                key.DeleteValue(name, throwOnMissingValue: false);
                return;
            }

            key.SetValue(name, ToValueObject(value, type), ToRegistryValueKind(type));
        }
        catch (Exception ex) when (
            ex is System.Security.SecurityException or
                UnauthorizedAccessException or
                System.IO.IOException)
        {
            RegistryIssue?.Invoke($"Access denied writing {path}\\{name}");
        }
    }

    private static object? TryReadValue(string path, string name)
    {
        using var key = OpenKey(path);

        if (key == null)
        {
            return null;
        }

        return key.GetValue(name);
    }

    private static RegistryKey? OpenKey(string configPath)
    {
        var (baseKey, subKey) = SplitPath(configPath);

        if (baseKey == null)
        {
            return null;
        }

        try
        {
            return baseKey.OpenSubKey(subKey);
        }
        catch
        {
            return null;
        }
    }

    private static RegistryKey? OpenOrCreateKey(string configPath)
    {
        var (baseKey, subKey) = SplitPath(configPath);

        if (baseKey == null)
        {
            return null;
        }

        return baseKey.CreateSubKey(subKey, writable: true);
    }

    private static (RegistryKey? Base, string SubKey) SplitPath(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            return (null, string.Empty);
        }

        string normalized = configPath.Replace('/', '\\');

        int idx = normalized.IndexOf('\\');
        string hive = idx > 0
            ? normalized[..idx].TrimEnd(':')
            : normalized.TrimEnd(':');
        string sub = idx > 0
            ? normalized[(idx + 1)..]
            : string.Empty;

        RegistryKey? baseKey = hive.ToUpperInvariant() switch
        {
            "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            "HKCU" or "HKEY_CURRENT_USER" => Registry.CurrentUser,
            "HKU" or "HKEY_USERS" => Registry.Users,
            "HKCR" or "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
            _ => null
        };

        return (baseKey, sub);
    }

    private static object ToValueObject(string? value, string? type)
    {
        if (type == "Binary")
        {
            return Convert.FromHexString(
                (value ?? "").Replace(",", "").Trim());
        }

        return value ?? string.Empty;
    }

    private static RegistryValueKind ToRegistryValueKind(string? type) =>
        type switch
        {
            "DWord" => RegistryValueKind.DWord,
            "QWord" => RegistryValueKind.QWord,
            "ExpandString" => RegistryValueKind.ExpandString,
            "MultiString" => RegistryValueKind.MultiString,
            "Binary" => RegistryValueKind.Binary,
            _ => RegistryValueKind.String
        };

    private static string Normalize(object? value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        if (value is byte[] bytes)
        {
            return Convert.ToHexString(bytes);
        }

        if (value is int || value is long || value is uint || value is ulong)
        {
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!;
        }

        return value.ToString() ?? string.Empty;
    }

    private async Task RunPowerShellAsync(string script, CancellationToken ct)
    {
        string tempFile = Path.Combine(
            Path.GetTempPath(),
            $"forge_tweak_{Guid.NewGuid():N}.ps1");

        await File.WriteAllTextAsync(tempFile, script, ct);

        try
        {
            await RunProcessAsync(
                "powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{tempFile}\"",
                ct);
        }
        finally
        {
            try
            {
                File.Delete(tempFile);
            }
            catch
            {
            }
        }
    }

    private async Task<string> RunCaptureAsync(string command, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {command}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi)!;
        _currentProcess = process;

        string output = await process.StandardOutput.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);
        _currentProcess = null;

        return output;
    }

    private async Task RunProcessAsync(string fileName, string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi)!;
        _currentProcess = process;

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                Log?.Invoke(this, e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                Log?.Invoke(this, e.Data);
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);
        _currentProcess = null;
    }

    public void Cancel()
    {
        try
        {
            if (_currentProcess != null &&
                !_currentProcess.HasExited)
            {
                _currentProcess.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }
}
