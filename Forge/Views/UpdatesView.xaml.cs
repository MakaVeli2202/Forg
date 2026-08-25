using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Forge.Views;

public partial class UpdatesView : UserControl
{
    private readonly List<(string Id, string Name)> _pending = new();
    private bool _busy;

    public UpdatesView()
    {
        InitializeComponent();
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        BtnCheckUpdates.IsEnabled = false;
        BtnUpdateAll.IsEnabled = false;

        SetStatus("Scanning installed apps with winget...", 0x9A9088);
        BeginActivity();

        _pending.Clear();

        string output = await RunWingetLiveAsync(
            "upgrade --include-unknown --accept-source-agreements");

        EndActivity();
        ParseOutdated(output);

        if (_pending.Count == 0)
        {
            SetStatus("Everything is up to date. Nothing to forge today.", 0x22C55E);
        }
        else
        {
            OutdatedList.Items.Clear();
            foreach (var app in _pending)
            {
                OutdatedList.Items.Add($"{app.Name}  ->  {app.Id}");
            }
            OutdatedList.Visibility = Visibility.Visible;

            SetStatus($"{_pending.Count} app(s) have updates available.", 0xFFB155);
            BtnUpdateAll.IsEnabled = true;
        }

        OpProgress.Visibility = Visibility.Collapsed;
        BtnCheckUpdates.IsEnabled = true;
        _busy = false;
    }

    private async void UpdateAll_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _pending.Count == 0) return;
        _busy = true;
        BtnCheckUpdates.IsEnabled = false;
        BtnUpdateAll.IsEnabled = false;

        BeginActivity();
        OutdatedList.Items.Clear();
        OutdatedList.Visibility = Visibility.Collapsed;

        int total = _pending.Count;
        int ok = 0, failed = 0;

        for (int i = 0; i < total; i++)
        {
            var (id, name) = _pending[i];
            int pct = (int)(i * 100.0 / total);

            SetStatus($"Upgrading {name} ({i + 1} of {total})...", 0x9A9088);
            ShowProgress(pct);

            bool success = await UpgradeSingleAsync(id, name);

            if (success) ok++; else failed++;
        }

        ShowProgress(100);
        EndActivity();

        string summary = failed == 0
            ? $"Done. {ok} app(s) upgraded successfully."
            : $"Finished with {failed} failure(s). {ok} upgraded OK.";

        SetStatus(summary + " Re-check for the latest state.",
            failed == 0 ? 0x22C55Eu : 0xF87171u);

        LogLine(summary);
        OpProgress.Visibility = Visibility.Collapsed;
        BtnCheckUpdates.IsEnabled = true;
        _busy = false;
    }

    private void WindowsUpdate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:windowsupdate",
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    private static readonly System.Text.RegularExpressions.Regex TableRowPattern =
        new(
            @"^(?<name>.+?)\s+(?<id>[A-Za-z0-9][A-Za-z0-9._\-]*)\s+" +
            @"\S+\s+\S+\s+(?<source>winget|msstore)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private void ParseOutdated(string output)
    {
        var lines = output
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        int dataStart = lines.FindIndex(line =>
            line.Contains("-----", StringComparison.OrdinalIgnoreCase));

        if (dataStart < 0)
        {
            return;
        }

        foreach (var line in lines.Skip(dataStart + 1))
        {
            var match = TableRowPattern.Match(line.Trim());

            if (!match.Success)
            {
                continue;
            }

            string id = match.Groups["id"].Value;
            string name = match.Groups["name"].Value;

            if (_pending.Any(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _pending.Add((id, name));
            LogLine($"Found update: {name} ({id})");
        }
    }

    private async Task<bool> UpgradeSingleAsync(string id, string name)
    {
        try
        {
            LogLine($"Starting upgrade: {name}...");

            var psi = new ProcessStartInfo
            {
                FileName = "winget",
                Arguments =
                    $"upgrade --id \"{id}\" --exact --silent --include-unknown " +
                    "--accept-package-agreements --accept-source-agreements",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi)!;

            process.OutputDataReceived += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    Dispatcher.BeginInvoke(() => LogLine("  " + args.Data.Trim()));
                }
            };
            process.ErrorDataReceived += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    Dispatcher.BeginInvoke(() => LogLine("  ! " + args.Data.Trim()));
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            bool ok = process.ExitCode == 0;
            LogLine(ok
                ? $"Finished: {name}"
                : $"Failed (exit {process.ExitCode}): {name}");
            return ok;
        }
        catch (Exception ex)
        {
            LogLine($"Error upgrading {name}: {ex.Message}");
            return false;
        }
    }

    private static async Task<string> RunWingetLiveAsync(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "winget",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;

        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output;
    }

    private void BeginActivity()
    {
        ActivityLog.Items.Clear();
        ActivityBorder.Visibility = Visibility.Visible;
        OpProgress.IsIndeterminate = true;
        OpProgress.Value = 0;
        OpProgress.Visibility = Visibility.Visible;
        ActivityLog.ScrollIntoView(ActivityLog.Items.Count > 0
            ? ActivityLog.Items[^1]
            : null);
    }

    private void EndActivity()
    {
        OpProgress.IsIndeterminate = false;
    }

    private void ShowProgress(int percent)
    {
        OpProgress.IsIndeterminate = false;
        OpProgress.Value = percent;
    }

    private void LogLine(string message)
    {
        ActivityLog.Items.Add($"[{DateTime.Now:HH:mm:ss}]  {message}");

        if (ActivityLog.Items.Count > 400)
        {
            ActivityLog.Items.RemoveAt(0);
        }

        if (ActivityLog.Items.Count > 0)
        {
            ActivityLog.ScrollIntoView(ActivityLog.Items[^1]);
        }
    }

    private void SetStatus(string message, uint rgb)
    {
        StatusText.Text = message;
        StatusText.Foreground = new SolidColorBrush(
            Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
    }
}
