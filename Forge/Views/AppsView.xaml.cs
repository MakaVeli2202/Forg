using Forge.Models;
using Forge.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Forge.Views;

public partial class AppsView : UserControl
{
    private List<AppItem> _apps = [];
    private List<AppItem> _visibleApps = [];
    private string _selectedCategory = "All";
    private string _currentOperation = string.Empty;

    private readonly InstallService _installService = new();
    private readonly DetectionService _detectionService = new();

    public AppsView()
    {
        InitializeComponent();

        Loaded += AppsView_Loaded;
        _installService.ProgressChanged += InstallService_ProgressChanged;
        _installService.OutputLine += InstallService_OutputLine;
        _installService.DefaultApplied += InstallService_DefaultApplied;
    }

    private async void AppsView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            LogInfo("Loading apps catalog.");

            _apps = AppService.LoadApps();

            LogInfo($"Loaded {_apps.Count} applications from the catalog.");

            foreach (var app in _apps)
            {
                if (app.Recommended)
                {
                    app.IsSelected = true;
                }
            }

            await _detectionService
                .DetectInstalledAppsAsync(_apps);

            LogInfo("Installed apps scan completed.");


            foreach (var app in _apps)
            {
                if (app.IsInstalled)
                {
                    app.Status = AppStatus.Installed;
                }
            }

            LoadApplications();
            UpdateStatistics();

            if (AppsHeaderText is not null)
            {
                AppsHeaderText.Text = $"Apps ({_apps.Count})";
            }

            SearchBox.TextChanged += SearchBox_TextChanged;

            CategoriesList.SelectedIndex = 0;
        }
        finally
        {
            SkeletonGrid.Visibility = Visibility.Collapsed;
            AppsContent.Visibility = Visibility.Visible;
        }
    }

    private void LoadApplications(
    IEnumerable<AppItem>? apps = null)
    {
        if (AppsList is null)
        {
            return;
        }

        _visibleApps = (apps ?? GetFilteredApps()).ToList();

        AppsList.ItemsSource = _visibleApps;

        if (EmptyState != null)
        {
            EmptyState.Visibility =
                _visibleApps.Count == 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }
    }

    private List<AppItem> GetFilteredApps()
    {
        IEnumerable<AppItem> filtered = _apps;

        switch (_selectedCategory)
        {
            case "Installed":
                filtered = filtered.Where(a => a.IsInstalled);
                break;

            case "Not Installed":
                filtered = filtered.Where(a => !a.IsInstalled);
                break;

            case "Recommended":
                filtered = filtered.Where(a => a.Recommended);
                break;

            case "All":
                break;

            default:
                filtered = filtered.Where(a =>
                    a.Category.Equals(
                        _selectedCategory,
                        StringComparison.OrdinalIgnoreCase));
                break;
        }

        if (!string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            filtered = filtered.Where(a =>
                a.Name.Contains(
                    SearchBox.Text,
                    StringComparison.OrdinalIgnoreCase));
        }

        return GetSortedApps(filtered.ToList());
    }

    private void SearchBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        PlaceholderText.Visibility =
            string.IsNullOrWhiteSpace(SearchBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

        FilterApps();
    }

    private void CategoriesList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (CategoriesList.SelectedItem is ListBoxItem item)
        {
            _selectedCategory =
                item.Content?.ToString() ?? "All";
            FilterApps();
        }
    }

    private void FilterApps()
    {
        LoadApplications(GetFilteredApps());
        UpdateStatistics();
    }

    private void SortComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        FilterApps();
    }

    private List<AppItem> GetSortedApps(List<AppItem> apps)
    {
        if (SortComboBox is null)
        {
            return apps;
        }

        return SortComboBox.SelectedIndex switch
        {
            0 => [.. apps.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)],
            1 => [.. apps.OrderByDescending(a => a.Name, StringComparer.OrdinalIgnoreCase)],
            2 => [.. apps.OrderBy(a => a.Category, StringComparer.OrdinalIgnoreCase)
                          .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)],
            3 => [.. apps.OrderBy(a => a.Publisher, StringComparer.OrdinalIgnoreCase)
                          .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)],
            4 => [.. apps.OrderByDescending(a => a.IsInstalled)
                          .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)],
            5 => [.. apps.OrderByDescending(a => a.Recommended)
                          .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)],
            _ => apps
        };
    }

    private async Task DetectInstalledAppsAsync(bool forceRefresh = false)
    {
        LogInfo(forceRefresh ? "Starting forced installed apps scan." : "Starting installed apps scan (using cache if available).");
        await _detectionService
            .DetectInstalledAppsAsync(_apps, forceRefresh);
        LogInfo("Installed apps scan completed.");

        foreach (var app in _apps)
        {
            if (app.IsInstalled)
            {
                app.Status = AppStatus.Installed;
            }
            else if (app.Status != AppStatus.Available)
            {
                app.Status = AppStatus.Available;
            }
        }

        LoadApplications();
        UpdateStatistics();
    }

    private async void BtnDetectInstalled_Click(
        object sender,
        RoutedEventArgs e)
    {
        AppsContent.Visibility = Visibility.Collapsed;
        SkeletonGrid.Visibility = Visibility.Visible;

        try
        {
            await DetectInstalledAppsAsync(forceRefresh: true);
            UpdateStatistics();
        }
        finally
        {
            SkeletonGrid.Visibility = Visibility.Collapsed;
            AppsContent.Visibility = Visibility.Visible;
        }
    }

    private async void BtnInstall_Click(
        object sender,
        RoutedEventArgs e)
    {
        var selectedApps = _apps
            .Where(a => a.IsSelected && !a.IsInstalled)
            .ToList();

        if (!selectedApps.Any())
        {
            LogWarning("Install requested with no applications selected.");

            MessageBox.Show(
                "Please select at least one application.");

            return;
        }

        try
        {
            _currentOperation = "Installing";

            LogInfo($"Install requested for {selectedApps.Count} app(s): {FormatAppList(selectedApps)}");

            BtnInstall.IsEnabled = false;
            BtnCancel.IsEnabled = true;

            foreach (var app in selectedApps)
            {
                app.Status = AppStatus.Installing;
            }

            UpdateStatus(
                "Installing applications...",
                0);

            await _installService
                .InstallAppsAsync(selectedApps);

            var failedApps = selectedApps
                .Where(a => a.Status == AppStatus.Failed)
                .ToList();

            if (failedApps.Any())
            {
                LogWarning($"Install completed with failures: {FormatAppList(failedApps)}");
            }
            else
            {
                LogInfo($"Install completed for {FormatAppList(selectedApps)}");
            }

            await DetectInstalledAppsAsync();
            foreach (var app in selectedApps)
            {
                app.IsSelected = false;
            }
            UpdateStatistics();
            UpdateStatus(
                "Installation completed.",
                100,
                true);
            
        }
        catch (Exception ex)
        {
            LogError($"Install failed: {ex.Message}");

            UpdateStatus(
                "Installation failed.",
                0);

            MessageBox.Show(
                ex.Message,
                "Installation Error");
        }
        finally
        {
            BtnInstall.IsEnabled = true;
            BtnCancel.IsEnabled = false;
            _currentOperation = string.Empty;
        }
    }

    private void BtnCancel_Click(
     object sender,
     RoutedEventArgs e)
    {
        LogWarning("Cancel requested for the current operation.");

        _installService.Cancel();

        foreach (var app in _apps.Where(a =>
            a.Status == AppStatus.Installing))
        {
            app.Status = AppStatus.Cancelling;
        }

        UpdateStatus(
            "Cancelling installation...",
            0);

        foreach (var app in _apps.Where(a =>
            a.Status == AppStatus.Cancelling))
        {
            app.Status = AppStatus.Available;
            app.IsSelected = false;
        }

        BtnInstall.IsEnabled = true;
        BtnCancel.IsEnabled = false;
        UpdateStatistics();
    }


    private async void UpdateStatus(
        string text,
        double progress,
        bool autoReset = false)
    {
        StatusText.Text = $"Status: {text}";
        InstallProgressBar.Value = progress;
        InstallProgressBar.Visibility =
            text.Equals("Ready", StringComparison.OrdinalIgnoreCase)
                ? Visibility.Collapsed
                : Visibility.Visible;

        if (autoReset)
        {
            await Task.Delay(1000);

            StatusText.Text = "Status: Ready";
            InstallProgressBar.Value = 0;
            InstallProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void BtnUpgrade_Click(
        object sender,
        RoutedEventArgs e)
    {
        var selectedApps = _apps
            .Where(a => a.IsSelected && a.IsInstalled)
            .ToList();

        if (!selectedApps.Any())
        {
            LogWarning("Upgrade requested with no installed applications selected.");

            MessageBox.Show(
                "Please select at least one installed application.");

            return;
        }

        try
        {
            _currentOperation = "Upgrading";

            LogInfo($"Upgrade requested for {selectedApps.Count} app(s): {FormatAppList(selectedApps)}");

            BtnUpgrade.IsEnabled = false;

            foreach (var app in selectedApps)
            {
                app.Status = AppStatus.Upgrading;
            }

            UpdateStatus(
                "Upgrading applications...",
                0);

            await _installService
                .UpgradeAppsAsync(selectedApps);

            var failedApps = selectedApps
                .Where(a => a.Status == AppStatus.Failed)
                .ToList();

            if (failedApps.Any())
            {
                LogWarning($"Upgrade completed with failures: {FormatAppList(failedApps)}");
            }
            else
            {
                LogInfo($"Upgrade completed for {FormatAppList(selectedApps)}");
            }

            await DetectInstalledAppsAsync();
            foreach (var app in selectedApps)
            {
                app.IsSelected = false;
            }
            UpdateStatistics();
            UpdateStatus(
                "Upgrade completed.",
                100,
                true);
            
        }
        catch (Exception ex)
        {
            LogError($"Upgrade failed: {ex.Message}");

            UpdateStatus(
                "Upgrade failed.",
                0);

            MessageBox.Show(
                ex.Message,
                "Upgrade Error");
        }
        finally
        {
            BtnUpgrade.IsEnabled = true;
            _currentOperation = string.Empty;
        }
    }

    private async void BtnUninstall_Click(
        object sender,
        RoutedEventArgs e)
    {
        var selectedApps = _apps
            .Where(a => a.IsSelected && a.IsInstalled)
            .ToList();

        if (!selectedApps.Any())
        {
            LogWarning("Uninstall requested with no installed applications selected.");

            MessageBox.Show(
                "Please select at least one installed application.");

            return;
        }

        string appList = string.Join(
            Environment.NewLine,
            selectedApps.Select(a => $"- {a.Name}"));

        var result = MessageBox.Show(
            $"The following applications will be removed:\n\n{appList}\n\nContinue?",
            "Confirm Uninstall",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            LogInfo($"Uninstall cancelled by user for {FormatAppList(selectedApps)}");

            return;
        }

        try
        {
            _currentOperation = "Uninstalling";

            LogInfo($"Uninstall confirmed for {FormatAppList(selectedApps)}");

            BtnUninstall.IsEnabled = false;

            foreach (var app in selectedApps)
            {
                app.Status = AppStatus.Uninstalling;
            }

            UpdateStatus(
                "Uninstalling applications...",
                0);

            await _installService
                .UninstallAppsAsync(selectedApps);

            var failedApps = selectedApps
                .Where(a => a.Status == AppStatus.Failed)
                .ToList();

            if (failedApps.Any())
            {
                LogWarning($"Uninstall completed with failures: {FormatAppList(failedApps)}");
            }
            else
            {
                LogInfo($"Uninstall completed for {FormatAppList(selectedApps)}");
            }

            await DetectInstalledAppsAsync();
            foreach (var app in selectedApps)
            {
                app.IsSelected = false;
            }
            UpdateStatistics();
            UpdateStatus(
                "Uninstall completed.",
                100,
                true);
            
        }
        catch (Exception ex)
        {
            LogError($"Uninstall failed: {ex.Message}");

            UpdateStatus(
                "Uninstall failed.",
                0);

            MessageBox.Show(
                ex.Message,
                "Uninstall Error");
        }
        finally
        {
            BtnUninstall.IsEnabled = true;
            _currentOperation = string.Empty;
        }
    }

    private void AppCard_MouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not Border border)
        {
            return;
        }

        if (border.DataContext is not AppItem app)
        {
            return;
        }

        app.IsSelected = !app.IsSelected;

        UpdateStatistics();

        e.Handled = true;
    }

    private void InstallService_ProgressChanged(
       object? sender,
       InstallProgressEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var percent =
                (double)e.Current / e.Total * 100;

            StatusText.Text =
                $"Status: {_currentOperation} {e.AppName} ({e.Current} of {e.Total})";

            LogInfo($"{_currentOperation} {e.AppName} ({e.Current} of {e.Total})");

            InstallProgressBar.IsIndeterminate = false;
            InstallProgressBar.Value = percent;
            InstallProgressBar.Visibility = Visibility.Visible;
        });
    }

    private void InstallService_OutputLine(
        object? sender,
        string line)
    {
        Dispatcher.Invoke(() =>
        {
            if (string.IsNullOrWhiteSpace(_currentOperation))
            {
                return;
            }

            InstallProgressBar.IsIndeterminate = true;

            Log($"      {line}");
        });
    }

    private void InstallService_DefaultApplied(
        object? sender,
        string message)
    {
        Dispatcher.Invoke(() =>
        {
            LogInfo($"DEFAULTS  {message}");
        });
    }

    private void UpdateStatistics()
    {
        TotalAppsText.Text =
            _apps.Count.ToString();

        InstalledAppsText.Text =
            _apps.Count(a => a.IsInstalled)
                 .ToString();

        SelectedAppsText.Text =
            _apps.Count(a => a.IsSelected)
                 .ToString();

        RecommendedAppsText.Text =
            _apps.Count(a => a.Recommended)
                 .ToString();

        UpdateSelectionButtons();
    }

    private void BtnSelectAll_Click(
    object sender,
    RoutedEventArgs e)
    {
        foreach (var app in _apps)
        {
            app.IsSelected = true;
        }

        UpdateStatistics();
    }

    private void BtnSelectRecommended_Click(
     object sender,
     RoutedEventArgs e)
    {
        foreach (var app in _apps)
        {
            app.IsSelected = app.Recommended;
        }

        UpdateStatistics();
    }

    private void BtnClearSelection_Click(
     object sender,
     RoutedEventArgs e)
    {
        foreach (var app in _apps)
        {
            app.IsSelected = false;
        }

        UpdateStatistics();
    }

    public void SetSectionVisibility(string sectionName, bool isVisible)
    {
        if (FindName(sectionName) is not FrameworkElement target)
        {
            return;
        }

        target.Visibility = isVisible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void Log(string message)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss}] {message}";

        System.Diagnostics.Debug.WriteLine(entry);

        if (ActivityLog is null)
        {
            return;
        }

        Dispatcher.Invoke(() =>
        {
            ActivityLog.AppendText(entry + Environment.NewLine);
            ActivityLog.ScrollToEnd();
        });
    }

    private void LogInfo(string message) => Log($"INFO  {message}");

    private void LogWarning(string message) => Log($"WARN  {message}");

    private void LogError(string message) => Log($"ERROR {message}");

    private static string FormatAppList(IEnumerable<AppItem> apps)
    {
        var names = apps
            .Select(app => app.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        return names.Count == 0
            ? "<none>"
            : string.Join(", ", names);
    }

    private void UpdateSelectionButtons()
    {
        // Null check for controls that may not be initialized yet
        if (BtnSelectAll == null || BtnSelectRecommended == null || BtnClearSelection == null)
        {
            return;
        }

        bool allSelected =
            _apps.Any() &&
            _apps.All(a => a.IsSelected);

        var recommendedApps = _apps.Where(a => a.Recommended).ToList();

        bool recommendedSelected =
            recommendedApps.Any() &&
            recommendedApps.Any(a => a.IsSelected) &&
            recommendedApps.Any(a => !a.IsSelected);

        BtnSelectAll.Background = CreateButtonBrush(
            allSelected ? "#F28C28" : "#261B12");
        BtnSelectAll.BorderBrush = CreateButtonBrush(
            allSelected ? "#FF9A2F" : "#3B4048");
        BtnSelectAll.Foreground = allSelected
            ? Brushes.White
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3E8"));

        BtnSelectRecommended.Background = CreateButtonBrush(
            recommendedSelected ? "#F28C28" : "#261B12");
        BtnSelectRecommended.BorderBrush = CreateButtonBrush(
            recommendedSelected ? "#FF9A2F" : "#3B4048");
        BtnSelectRecommended.Foreground = recommendedSelected
            ? Brushes.White
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3E8"));

        BtnClearSelection.Background = CreateButtonBrush("#261B12");
        BtnClearSelection.BorderBrush = CreateButtonBrush("#3B4048");
        BtnClearSelection.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3E8"));
    }

    private static SolidColorBrush CreateButtonBrush(string color)
    {
        var brush = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(color));

        brush.Freeze();
        return brush;
    }

}