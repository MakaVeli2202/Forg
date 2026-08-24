using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Forge.Views;

public partial class UpdatesView : UserControl
{
    public UpdatesView()
    {
        InitializeComponent();
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        BtnCheckUpdates.IsEnabled = false;
        BtnUpdateAll.IsEnabled = false;


        StatusText.Text = "Scanning installed apps with winget...";
        StatusText.Foreground = new SolidColorBrush(
            Color.FromRgb(0xB3, 0x9B, 0x85));

        OutdatedList.Items.Clear();
        OutdatedList.Visibility = Visibility.Collapsed;

        string output = await RunWingetAsync("upgrade --include-unknown");

        var lines = output
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        int dataStart = lines.FindIndex(line =>
            line.Contains("-----", StringComparison.OrdinalIgnoreCase));

        var outdated = dataStart >= 1
            ? lines.Skip(dataStart + 1)
                .Where(l => !l.StartsWith("The following packages", StringComparison.OrdinalIgnoreCase) &&
                            !l.Contains("packages have upgrade", StringComparison.OrdinalIgnoreCase) &&
                            !l.Contains("upgrade available", StringComparison.OrdinalIgnoreCase))
                .ToList()
            : [];

        if (dataStart < 0 || outdated.Count == 0)
        {
            StatusText.Text = "Everything is up to date. Nothing to forge today.";
            StatusText.Foreground = new SolidColorBrush(
                Color.FromRgb(0x22, 0xC5, 0x5E));
        }
        else
        {
            foreach (var line in outdated)
            {
                OutdatedList.Items.Add(line);
            }

            OutdatedList.Visibility = Visibility.Visible;

            StatusText.Text = $"{outdated.Count} app(s) have updates available.";
            StatusText.Foreground = new SolidColorBrush(
                Color.FromRgb(0xFF, 0xB1, 0x55));


            BtnUpdateAll.IsEnabled = true;
        }

        BtnCheckUpdates.IsEnabled = true;
    }

    private async void UpdateAll_Click(object sender, RoutedEventArgs e)
    {
        BtnCheckUpdates.IsEnabled = false;
        BtnUpdateAll.IsEnabled = false;

        StatusText.Text = "Upgrading all apps - this can take a while...";
        StatusText.Foreground = new SolidColorBrush(
            Color.FromRgb(0xB3, 0x9B, 0x85));

        await RunWingetAsync(
            "upgrade --all --silent --include-unknown --accept-package-agreements --accept-source-agreements");

        StatusText.Text = "Upgrade pass finished. Re-check for the latest state.";
        StatusText.Foreground = new SolidColorBrush(
            Color.FromRgb(0x22, 0xC5, 0x5E));

        OutdatedList.Items.Clear();
        OutdatedList.Visibility = Visibility.Collapsed;

        BtnCheckUpdates.IsEnabled = true;
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

    private static async Task<string> RunWingetAsync(string arguments)
    {
        using var process = new Process();

        process.StartInfo = new ProcessStartInfo
        {
            FileName = "winget",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        process.Start();

        string output =
            await process.StandardOutput.ReadToEndAsync();

        await process.WaitForExitAsync();

        return output;
    }
}
