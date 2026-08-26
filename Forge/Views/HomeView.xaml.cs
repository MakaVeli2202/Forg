using Forge.Models;
using Forge.Services;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Forge.Views;

public partial class HomeView : UserControl
{
    private const int MaxListItems = 16;

    public HomeView()
    {
        InitializeComponent();

        Loaded += (_, _) => LoadContent();
    }

    private void LoadContent()
    {
        string configDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Config");

        var apps = AppService.LoadApps();

        var recommended = apps
            .Where(app => app.Recommended)
            .ToList();

        RecommendedAppsList.ItemsSource = recommended.Take(MaxListItems).ToList();

        RecommendedSummaryText.Text = recommended.Count == 0
            ? "Nothing picked yet."
            : $"{recommended.Count} hand-picked apps - the essentials for every PC.";

        StatAppsText.Text = $"{apps.Count} apps in catalog";

        List<TweakItem> tweaks = [];

        try
        {
            using var doc = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(configDir, "tweaks.json")));

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    string? category = element.TryGetProperty("category", out var catEl)
                        ? catEl.GetString()
                        : null;

                    if (!string.Equals(category, "Essential Tweaks", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    tweaks.Add(new TweakItem
                    {
                        Id = element.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "",
                        Name = element.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : ""
                    });
                }
            }
        }
        catch
        {
        }

        EssentialTweaksList.ItemsSource = tweaks.Take(MaxListItems).ToList();

        EssentialSummaryText.Text = tweaks.Count == 0
            ? "Catalog unavailable."
            : $"{tweaks.Count} safe, reversible tweaks - apply or undo in one click.";

        StatTweaksText.Text = $"{CountArray(Path.Combine(configDir, "tweaks.json"))} tweaks ready";

        _ = LoadSystemInfoAsync();
    }

    private async Task LoadSystemInfoAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                string version = SystemInfoHelper.GetWindowsVersion();
                Dispatcher.Invoke(() => StatusWindows.Text = version);
            }
            catch
            {
                Dispatcher.Invoke(() => StatusWindows.Text = "Windows 11");
            }

            try
            {
                var (cpuName, cores) = SystemInfoHelper.GetCpuInfo();
                Dispatcher.Invoke(() => StatusCpu.Text = cores > 0 ? $"{cpuName} ({cores} cores)" : cpuName);
            }
            catch
            {
                Dispatcher.Invoke(() => StatusCpu.Text = "Detecting...");
            }

            try
            {
                string gpu = SystemInfoHelper.GetGpuInfo();
                Dispatcher.Invoke(() => StatusGpu.Text = gpu);
            }
            catch
            {
                Dispatcher.Invoke(() => StatusGpu.Text = "Unknown GPU");
            }

            try
            {
                var (totalGB, speedMHz) = SystemInfoHelper.GetMemoryInfo();
                Dispatcher.Invoke(() => StatusMemory.Text = speedMHz > 0 ? $"{totalGB} GB @ {speedMHz} MHz" : $"{totalGB} GB");
            }
            catch
            {
                Dispatcher.Invoke(() => StatusMemory.Text = "Detecting...");
            }

            try
            {
                var (totalGB, freeGB) = SystemInfoHelper.GetStorageInfo();
                Dispatcher.Invoke(() =>
                {
                    string free = freeGB >= 1024 ? $"{freeGB / 1024.0:F1} TB" : $"{freeGB:F1} GB";
                    string total = totalGB >= 1024 ? $"{totalGB / 1024.0:F1} TB" : $"{totalGB:F1} GB";
                    StatusStorage.Text = $"{free} free / {total} total";
                });
            }
            catch
            {
                Dispatcher.Invoke(() => StatusStorage.Text = "Detecting...");
            }
        });
    }

    private static int CountArray(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));

            return doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.GetArrayLength()
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string page)
        {
            (Window.GetWindow(this) as MainWindow)?.NavigateTo(page);
        }
    }

    private void RecommendedApps_Click(object sender, RoutedEventArgs e) =>
        (Window.GetWindow(this) as MainWindow)?.NavigateTo("apps");

    private void EssentialTweaks_Click(object sender, RoutedEventArgs e) =>
        (Window.GetWindow(this) as MainWindow)?.NavigateTo("tweaks");

    private void QuickSetup_Click(object sender, RoutedEventArgs e)
    {
        var apps = AppService.LoadApps();
        int recommendedCount = apps.Count(a => a.Recommended);

        var result = MessageBox.Show(
            $"QUICK SETUP\n\n" +
            $"This will set up your PC with recommended settings:\n\n" +
            $"  APPS ({recommendedCount} recommended)\n" +
            $"  Install all recommended applications including:\n" +
            $"  Chrome, Discord, Spotify, Steam, VLC, 7-Zip,\n" +
            $"  PowerToys, Notepad++, Wallpaper Engine, and more.\n\n" +
            $"  TWEAKS\n" +
            $"  Apply essential performance & privacy tweaks\n" +
            $"  Enable Ultimate Performance power plan\n" +
            $"  Disable hibernation, telemetry, and background apps\n\n" +
            $"This process may take several minutes.\n" +
            $"A system restart is recommended afterward.\n\n" +
            $"Continue with Quick Setup?",
            "Forge Quick Setup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        (Window.GetWindow(this) as MainWindow)?.NavigateTo("apps");
    }
}
