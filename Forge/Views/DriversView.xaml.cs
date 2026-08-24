using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace Forge.Views;

public partial class DriversView : UserControl
{
    private record DeviceInfo(
        string Name,
        string PnpId,
        string Class,
        int ErrorCode,
        bool Present);

    private readonly List<DeviceInfo> _devices = new();
    private bool _scanning;

    public DriversView()
    {
        InitializeComponent();
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (_scanning) return;
        _scanning = true;
        BtnScan.IsEnabled = false;

        await ScanCoreAsync();

        BtnScan.IsEnabled = true;
        _scanning = false;
    }

    private async Task ScanCoreAsync()
    {
        StatusText.Text = "Scanning hardware devices via WMI...";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xB3, 0x9B, 0x85));
        DeviceList.Items.Clear();

        _devices.Clear();

        try
        {
            string json = await Task.Run(() => QueryDevices());

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            IEnumerable<JsonElement> items =
                root.ValueKind == JsonValueKind.Array
                    ? root.EnumerateArray()
                    : new[] { root.Clone() };

            foreach (var item in items)
            {
                string name = item.TryGetProperty("Name", out var n) && n.ValueKind == JsonValueKind.String
                    ? n.GetString() ?? ""
                    : "";

                if (string.IsNullOrWhiteSpace(name))
                {
                    name = "(Unknown device)";
                }

                string pnp = item.TryGetProperty("PNPDeviceID", out var p) && p.ValueKind == JsonValueKind.String
                    ? p.GetString() ?? ""
                    : "";

                string cls = item.TryGetProperty("PNPClass", out var c) && c.ValueKind == JsonValueKind.String &&
                             !string.IsNullOrWhiteSpace(c.GetString())
                    ? c.GetString()!
                    : "Other";

                int code = item.TryGetProperty("ConfigManagerErrorCode", out var ec) &&
                           ec.ValueKind == JsonValueKind.Number
                    ? ec.GetInt32()
                    : 0;

                bool present = true;
                if (item.TryGetProperty("Present", out var pr) &&
                    pr.ValueKind == JsonValueKind.False)
                {
                    present = false;
                }

                if (!present)
                {
                    continue;
                }

                _devices.Add(new DeviceInfo(name, pnp, cls, code, present));
            }

            RenderList();
            DetectOemTools();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Scan failed: {ex.Message}";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));
        }
    }

    private async void FixAll_Click(object sender, RoutedEventArgs e)
    {
        if (_scanning) return;
        _scanning = true;
        BtnFixAll.IsEnabled = false;
        BtnScan.IsEnabled = false;

        try
        {
            LogRow("Starting one-click driver fix...");

            SetMsg("Step 1 of 4 - Creating a restore point...", 0xB39B85);
            await CreateRestorePointCoreAsync();
            LogRow("Restore point ready.");

            SetMsg("Step 2 of 4 - Scanning all devices...", 0xB39B85);
            await ScanCoreAsync();

            int problems = _devices.Count(d => d.ErrorCode != 0);

            if (problems == 0)
            {
                SetMsg("All drivers are healthy - nothing to fix.", 0x22C55E);
                LogRow("No problem devices found.");
                return;
            }

            foreach (var d in _devices.Where(d => d.ErrorCode != 0))
            {
                LogRow($"Problem: {d.Name} - {DescribeCode(d.ErrorCode)}");
            }

            SetMsg($"Step 3 of 4 - Asking Windows Update for driver fixes ({problems} device(s))...", 0xB39B85);
            LogRow("Searching Windows Update for WHQL driver updates - this can take a few minutes.");

            var outcome = await RunWindowsUpdateDriverFixAsync();

            SetMsg("Step 4 of 4 - Re-scanning devices to verify...", 0xB39B85);
            await ScanCoreAsync();

            int remaining = _devices.Count(d => d.ErrorCode != 0);

            if (outcome.Installed > 0 && remaining == 0)
            {
                SetMsg($"Fixed! Installed {outcome.Installed} driver update(s). All devices now healthy.", 0x22C55E);
            }
            else if (outcome.Installed > 0)
            {
                SetMsg($"Installed {outcome.Installed} driver update(s). {remaining} device(s) still need manual attention (no WU driver available).", 0xFFB155);
            }
            else if (outcome.Searched)
            {
                SetMsg($"{remaining} device(s) need attention, but Windows Update has no driver for them. Try the manufacturer tools below.", 0xFFB155);
            }
            else
            {
                SetMsg("Could not reach Windows Update. Check your internet connection and try again.", 0xF87171);
            }
        }
        finally
        {
            BtnFixAll.IsEnabled = true;
            BtnScan.IsEnabled = true;
            _scanning = false;
        }
    }

    private void SetMsg(string message, uint rgb)
    {
        StatusText.Text = message;
        StatusText.Foreground = new SolidColorBrush(
            Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
    }

    private void LogRow(string message)
    {
        DeviceList.Items.Add(new TextBlock
        {
            Text = $"[{DateTime.Now:HH:mm:ss}]  {message}",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11.5,
            Foreground = new SolidColorBrush(Color.FromRgb(0xB3, 0x9B, 0x85)),
            Margin = new Thickness(2, 1, 0, 1),
            TextWrapping = TextWrapping.Wrap
        });

        if (DeviceList.Items.Count > 0)
        {
            DeviceList.ScrollIntoView(DeviceList.Items[^1]);
        }
    }

    private async Task<(bool Searched, int Installed)> RunWindowsUpdateDriverFixAsync()
    {
        string scriptDir = Path.Combine(Path.GetTempPath(), "Forge");
        Directory.CreateDirectory(scriptDir);

        string scriptPath = Path.Combine(scriptDir, "FixDrivers.ps1");

        const string script = """
$ErrorActionPreference = 'Stop'
try {
    $session = New-Object -ComObject Microsoft.Update.Session
    $searcher = $session.CreateUpdateSearcher()
    Write-Output "PHASE:SEARCHING"
    $result = $searcher.Search("IsInstalled=0 and Type='Driver'")
    if ($result.Updates.Count -eq 0) { Write-Output "COUNT:0"; exit 0 }
    Write-Output "COUNT:$($result.Updates.Count)"

    $coll = New-Object -ComObject Microsoft.Update.UpdateColl
    foreach ($u in $result.Updates) {
        if (-not $u.EulaAccepted) { [void]$u.AcceptEula() }
        [void]$coll.Add($u)
        Write-Output "FOUND:$($u.Title)"
    }

    Write-Output "PHASE:DOWNLOADING"
    $downloader = $session.CreateUpdateDownloader()
    $downloader.Updates = $coll
    [void]$downloader.Download()

    Write-Output "PHASE:INSTALLING"
    $installer = $session.CreateUpdateInstaller()
    $installer.Updates = $coll
    $ires = $installer.Install()
    Write-Output "RESULT:$($ires.ResultCode)"
    exit 0
} catch {
    Write-Output "ERROR:$($_.Exception.Message)"
    exit 1
}
""";

        await File.WriteAllTextAsync(scriptPath, script);

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        int found = 0;
        bool searched = false;

        var tcs = new TaskCompletionSource<int>();

        using var process = Process.Start(psi)!;

        process.OutputDataReceived += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(args.Data)) return;
            string line = args.Data.Trim();

            Dispatcher.BeginInvoke(() =>
            {
                if (line.StartsWith("FOUND:", StringComparison.Ordinal))
                {
                    found++;
                    LogRow($"Driver fix available: {line[6..]}");
                }
                else if (line.StartsWith("PHASE:", StringComparison.Ordinal))
                {
                    string phase = line[6..];
                    SetMsg($"Windows Update - {phase.ToLowerInvariant()} drivers...", phase == "SEARCHING" ? 0xB39B85u : 0xFFB155u);
                    if (phase == "SEARCHING") searched = true;
                }
                else if (line.StartsWith("COUNT:", StringComparison.Ordinal))
                {
                    searched = true;
                    LogRow($"Windows Update offers {line[6..]} driver update(s).");
                }
                else if (line.StartsWith("RESULT:", StringComparison.Ordinal))
                {
                    LogRow(line == "RESULT:2" || line == "RESULT:3"
                        ? "Installation finished successfully."
                        : $"Installation ended with result code {line[7..]}.");
                }
                else
                {
                    LogRow(line);
                }
            });
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                Dispatcher.BeginInvoke(() => LogRow("! " + args.Data.Trim()));
            }
        };

        process.Exited += (_, _) => tcs.TrySetResult(process.ExitCode);
        process.EnableRaisingEvents = true;

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await tcs.Task;

        try { File.Delete(scriptPath); } catch { }

        return (searched, found);
    }

    private async Task CreateRestorePointCoreAsync()
    {
        await Task.Run(() =>
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments =
                    "-NoProfile -ExecutionPolicy Bypass -Command " +
                    "\"Checkpoint-Computer -Description 'Forge Driver Work' -RestorePointType MODIFY_SETTINGS\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            p.WaitForExit(120000);
        });
    }

    private static string QueryDevices()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments =
                "-NoProfile -ExecutionPolicy Bypass -Command " +
                "\"Get-CimInstance -ClassName Win32_PnPEntity | Select-Object Name,PNPDeviceID,PNPClass,ConfigManagerErrorCode,Present | ConvertTo-Json -Compress -Depth 2\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(30000);
        return output;
    }

    private void RenderList()
    {
        DeviceList.Items.Clear();

        string filter = (FilterBox.SelectedItem as ComboBoxItem)?.Content as string ?? "All devices";

        var shown = _devices
            .Where(d => filter == "All devices" || d.ErrorCode == 0)
            .Where(d =>
            {
                if (filter == "All devices") return true;
                if (filter == "Needs attention only") return d.ErrorCode != 0;
                return Classify(d.Class) == filter;
            })
            .OrderByDescending(d => d.ErrorCode != 0)
            .ThenBy(d => Classify(d.Class))
            .ThenBy(d => d.Name)
            .ToList();

        int problems = _devices.Count(d => d.ErrorCode != 0);

        foreach (var d in shown)
        {
            bool bad = d.ErrorCode != 0;

            var row = new Border
            {
                Background = new SolidColorBrush(bad
                    ? Color.FromRgb(0x2A, 0x14, 0x10)
                    : Color.FromArgb(0, 0, 0, 0)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 7, 10, 7),
                Margin = new Thickness(0, 1, 0, 1)
            };

            var grid = new Grid { };

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });

            var glyph = new TextBlock
            {
                Text = bad ? "\u26A0" : "\u2713",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(bad
                    ? Color.FromRgb(0xF8, 0x71, 0x71)
                    : Color.FromRgb(0x22, 0xC5, 0x5E)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(glyph, 0);

            var nameBlock = new TextBlock
            {
                Text = d.Name,
                Foreground = new SolidColorBrush(bad
                    ? Color.FromRgb(0xFF, 0xF3, 0xE8)
                    : Color.FromRgb(0xC9, 0xB4, 0xA0)),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = $"{d.Name}\n{d.PnpId}"
            };
            Grid.SetColumn(nameBlock, 1);

            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x24, 0x18, 0x0E)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x2C, 0x1D)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 2, 8, 2),
                Child = new TextBlock
                {
                    Text = Classify(d.Class).ToUpperInvariant(),
                    FontSize = 9.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xB3, 0x9B, 0x85)),
                    VerticalAlignment = VerticalAlignment.Center
                },
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(badge, 2);

            var spacer = new TextBlock();
            Grid.SetColumn(spacer, 3);

            var statusBlock = new TextBlock
            {
                Text = DescribeCode(d.ErrorCode),
                FontSize = 11,
                Foreground = new SolidColorBrush(bad
                    ? Color.FromRgb(0xF8, 0x71, 0x71)
                    : Color.FromRgb(0x6E, 0x5B, 0x49)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(statusBlock, 4);

            grid.Children.Add(glyph);
            grid.Children.Add(nameBlock);
            grid.Children.Add(badge);
            grid.Children.Add(spacer);
            grid.Children.Add(statusBlock);

            row.Child = grid;
            DeviceList.Items.Add(row);
        }

        ChipTotal.Text = _devices.Count.ToString();
        ChipProblem.Text = problems.ToString();
        ChipOk.Text = (_devices.Count - problems).ToString();

        StatusText.Text = problems > 0
            ? $"{shown.Count} device(s) shown - {problems} need attention (yellow warning in Device Manager)."
            : $"All {_devices.Count} present devices report healthy driver state.";

        StatusText.Foreground = new SolidColorBrush(problems > 0
            ? Color.FromRgb(0xFF, 0xB1, 0x55)
            : Color.FromRgb(0x22, 0xC5, 0x5E));
    }

    private static string Classify(string pnpClass)
    {
        switch ((pnpClass ?? "").ToLowerInvariant())
        {
            case "display":
                return "Display";
            case "media":
            case "audioendpoint":
            case "audioprocessor":
                return "Audio";
            case "bluetooth":
                return "Bluetooth";
            case "net":
                return "Network";
            case "usb":
                return "USB";
            case "diskdrive":
            case "hdc":
            case "scsiadapter":
            case "volume":
                return "Storage";
            case "mouse":
            case "keyboard":
            case "hidclass":
                return "Input";
            default:
                return "Other";
        }
    }

    private static string DescribeCode(int code)
    {
        return code switch
        {
            0 => "Working correctly",
            1 => "Not configured correctly",
            3 => "Driver is corrupted",
            10 => "Device cannot start",
            12 => "Not enough resources",
            14 => "Restart required",
            18 => "Drivers must be reinstalled",
            19 => "Registry configuration corrupted",
            21 => "Windows is removing this device",
            22 => "Device is disabled",
            24 => "Device is not working",
            28 => "DRIVERS NOT INSTALLED",
            31 => "Not working properly",
            37 => "Driver initialization failed",
            39 => "Driver files corrupted",
            43 => "Stopped due to a problem",
            45 => "Device not connected",
            _ => $"Problem (code {code})"
        };
    }

    private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_devices.Count > 0)
        {
            RenderList();
        }
    }

    private void WindowsUpdate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:windowsupdate-optionalupdates",
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    private async void RestorePoint_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "Creating restore point...";
            await CreateRestorePointCoreAsync();
            StatusText.Text = "Restore point created (or already created within the last 24h).";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Restore point failed: {ex.Message}";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));
        }
    }

    private void DeviceManager_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "devmgmt.msc",
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    private void DetectOemTools()
    {
        var candidates = new (string Match, string Label)[]
        {
            ("Lenovo Vantage", "Lenovo Vantage"),
            ("Commercial Vantage", "Lenovo Vantage"),
            ("Dell Command Update", "Dell Command Update"),
            ("SupportAssist", "Dell SupportAssist"),
            ("HP Support Assistant", "HP Support Assistant"),
            ("Intel Driver & Support Assistant", "Intel DSA"),
            ("NVIDIA App", "NVIDIA App"),
            ("GeForce Experience", "GeForce Experience"),
            ("AMD Software", "AMD Adrenalin")
        };

        var found = new List<(string Label, string ExePath)>();

        string[] roots =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        foreach (var root in roots)
        {
            using var key = Registry.LocalMachine.OpenSubKey(root);
            if (key == null) continue;

            foreach (var sub in key.GetSubKeyNames())
            {
                using var item = key.OpenSubKey(sub);
                if (item == null) continue;

                string name = item.GetValue("DisplayName") as string ?? "";
                if (string.IsNullOrWhiteSpace(name)) continue;

                foreach (var (matchPart, label) in candidates)
                {
                    if (!name.Contains(matchPart, StringComparison.OrdinalIgnoreCase)) continue;

                    string rawIcon = item.GetValue("DisplayIcon") as string ?? "";
                    string exe = rawIcon.Split(',')[0].Trim().Trim('"');

                    if (exe.Length > 0 && File.Exists(exe))
                    {
                        if (!found.Any(f => f.Label == label))
                        {
                            found.Add((label, exe));
                        }
                    }

                    break;
                }
            }
        }

        OemTools.Children.Clear();

        if (found.Count == 0)
        {
            OemPanel.Visibility = Visibility.Collapsed;
            return;
        }

        foreach (var (label, exe) in found.Take(6))
        {
            var btn = new Button
            {
                Content = $"Open {label}",
                Style = (Style)FindResource("ForgeButtonStyle"),
                Margin = new Thickness(0, 0, 10, 0)
            };
            btn.Click += (_, _) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exe,
                        UseShellExecute = true
                    });
                }
                catch
                {
                }
            };
            OemTools.Children.Add(btn);
        }

        OemPanel.Visibility = Visibility.Visible;
    }
}
