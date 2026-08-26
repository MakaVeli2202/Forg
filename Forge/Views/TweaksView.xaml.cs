using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Forge.Models;
using Forge.Services;

namespace Forge.Views;

public partial class TweaksView : UserControl
{
    private const string UltimatePerfId = "UltPerf";

    private readonly TweakService _tweakService = new();
    private ICollectionView? _view;
    private bool _isBusy;
    private bool _suppressComboEvents;

    public TweaksView()
    {
        InitializeComponent();

        Loaded += TweaksView_Loaded;
        _tweakService.Log += TweakService_Log;
    }

    private void TweaksView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (_view != null)
        {
            return;
        }

        var tweaks = _tweakService.LoadTweaks();

        foreach (var tweak in tweaks)
        {
            tweak.IsApplied = SafeState(tweak);
        }

        TweaksList.ItemsSource = tweaks;

        _view = CollectionViewSource.GetDefaultView(tweaks);

        _view.GroupDescriptions.Add(
            new PropertyGroupDescription(nameof(TweakItem.Group)));

        _view.Filter = FilterTweak;

        SearchBox.TextChanged += SearchBox_TextChanged;
    }

    private bool? SafeState(TweakItem tweak)
    {
        try
        {
            if (tweak.Id == UltimatePerfId)
            {
                return _tweakService.GetUltimatePerformanceActive();
            }

            return _tweakService.GetAppliedState(tweak);
        }
        catch
        {
            return null;
        }
    }

    private bool FilterTweak(object item)
    {
        if (item is not TweakItem tweak)
        {
            return false;
        }

        string group = SelectedGroupName();

        if (group.Length > 0 &&
            !string.Equals(tweak.Group, group, StringComparison.Ordinal))
        {
            return false;
        }

        string query = SearchBox.Text.Trim();

        if (query.Length == 0)
        {
            return true;
        }

        return tweak.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               (tweak.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
               tweak.Group.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private string SelectedGroupName() =>
        GroupFilterList.SelectedItem is ListBoxItem item &&
        item.Tag is string tag
            ? tag
            : string.Empty;

    private void GroupFilterList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        _view?.Refresh();
    }

    private void SearchBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility =
            SearchBox.Text.Length == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

        _view?.Refresh();
    }

    private void ComboSelector_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not ComboBox combo ||
            combo.Tag is not TweakItem tweak ||
            tweak.ComboItems == null)
        {
            return;
        }

        _suppressComboEvents = true;

        try
        {
            string? current = null;

            if (tweak.Registry is { Count: > 0 })
            {
                try
                {
                    current = _tweakService.GetComboState(tweak);
                }
                catch
                {
                    current = null;
                }
            }

            combo.SelectedItem = current ?? tweak.ComboItems[0];
        }
        finally
        {
            _suppressComboEvents = false;
        }
    }

    private async void ComboSelector_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_suppressComboEvents ||
            _isBusy ||
            sender is not ComboBox combo ||
            combo.Tag is not TweakItem tweak ||
            combo.SelectedItem is not string state)
        {
            return;
        }

        await RunExclusiveAsync(
            $"Setting {tweak.Name}: {state}",
            ct => _tweakService.ApplyComboAsync(tweak, state, ct),
            () => tweak.IsApplied = SafeState(tweak));
    }

    private async void RunTweakButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_isBusy ||
            sender is not Button button ||
            button.Tag is not TweakItem tweak)
        {
            return;
        }

        switch (tweak.Id)
        {
            case "OOSUbutton":
                await RunExclusiveAsync(
                    "Downloading O&O ShutUp10++...",
                    ct => _tweakService.RunOosuAsync(ct));
                break;

            default:
                await RunExclusiveAsync(
                    $"Running {tweak.Name}...",
                    ct => _tweakService.ApplyAsync(tweak, ct),
                    () => tweak.IsApplied = SafeState(tweak));
                break;
        }
    }

    private void ApplyToggle_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not TweakItem tweak)
        {
            return;
        }

        RefreshToggleLabel(button, tweak);
    }

    private async void ApplyToggle_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_isBusy ||
            sender is not Button button ||
            button.Tag is not TweakItem tweak)
        {
            return;
        }

        if (tweak.Id == UltimatePerfId)
        {
            bool enable = tweak.IsApplied != true;

            await RunExclusiveAsync(
                enable
                    ? "Enabling Ultimate Performance plan..."
                    : "Restoring default power plans...",
                ct => _tweakService.SetUltimatePerformanceAsync(enable, ct),
                () => RefreshToggleLabel(button, tweak));

            return;
        }

        bool undo = tweak.IsApplied == true;

        await RunExclusiveAsync(
            undo
                ? $"Undoing: {tweak.Name}"
                : $"Applying: {tweak.Name}",
            ct => undo
                ? _tweakService.UndoAsync(tweak, ct)
                : _tweakService.ApplyAsync(tweak, ct),
            () =>
            {
                tweak.IsApplied = SafeState(tweak);
                RefreshToggleLabel(button, tweak);
            });
    }

    private void RefreshToggleLabel(
        Button button,
        TweakItem tweak)
    {
        bool applied = tweak.Id == UltimatePerfId
            ? SafeState(tweak) == true
            : tweak.IsApplied == true;

        button.Content = applied ? "Undo" : "Apply";
        button.Background = applied
            ? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#0078D4")
            : (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#FF7A00");
    }

    private async Task RunExclusiveAsync(
        string statusMessage,
        Func<CancellationToken, Task> action,
        Action? onComplete = null)
    {
        BeginBusy(statusMessage);

        try
        {
            await action(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            LogLine("Cancelled.");
        }
        catch (Exception ex)
        {
            LogLine($"Error: {ex.Message}");
        }
        finally
        {
            onComplete?.Invoke();
            EndBusy();
        }
    }

    private void BeginBusy(string message)
    {
        _isBusy = true;
        SetStatus(message);
        SetButtonsEnabled(false);
    }

    private void EndBusy()
    {
        _isBusy = false;
        SetButtonsEnabled(true);
        SetStatus("Ready");
    }

    private void SetButtonsEnabled(bool enabled)
    {
        BtnCancel.IsEnabled = !enabled;
    }

    private void SetStatus(string message) =>
        StatusText.Text = message;

    private void LogLine(string message)
    {
        ActivityLog.AppendText(message + Environment.NewLine);
        ActivityLog.ScrollToEnd();
    }

    private void TweakService_Log(
        object? sender,
        string message)
    {
        Dispatcher.BeginInvoke(() => LogLine(message));
    }

    private void BtnCancel_Click(
        object sender,
        RoutedEventArgs e)
    {
        _tweakService.Cancel();
    }

    private async void BtnGamingPreset_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        var result = MessageBox.Show(
            "Apply the Gaming Preset?\n\n" +
            "This will apply these recommended tweaks for gaming:\n" +
            "- Game Mode (ON)\n" +
            "- Ultimate Performance Profile\n" +
            "- Hibernation (OFF)\n" +
            "- Background Apps (OFF)\n" +
            "- Delivery Optimization (OFF)\n" +
            "- Services (Set to Manual)\n" +
            "- Visual Effects (Best Performance)\n\n" +
            "Continue?",
            "Gaming Preset",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        string[] gamingTweakIds =
        [
            "GameMode",
            "AddUltPerf",
            "Hiber",
            "DisableBGapps",
            "DeliveryOptimization",
            "Services",
            "Display"
        ];

        var tweaks = _tweakService.LoadTweaks();

        var selected = tweaks
            .Where(t => gamingTweakIds.Contains(t.Id))
            .ToList();

        BeginBusy($"Applying Gaming Preset ({selected.Count} tweaks)...");

        try
        {
            foreach (var tweak in selected)
            {
                if (tweak.Id == "AddUltPerf")
                {
                    bool active = _tweakService.GetUltimatePerformanceActive();
                    if (!active)
                    {
                        LogLine($"Applying: {tweak.Name}");
                        await _tweakService.SetUltimatePerformanceAsync(true, CancellationToken.None);
                    }
                    continue;
                }

                if (tweak.IsApplied == true)
                {
                    LogLine($"Already applied: {tweak.Name}");
                    continue;
                }

                LogLine($"Applying: {tweak.Name}");
                await _tweakService.ApplyAsync(tweak, CancellationToken.None);
                tweak.IsApplied = SafeState(tweak);
            }

            LogLine("Gaming Preset applied successfully.");
        }
        catch (OperationCanceledException)
        {
            LogLine("Cancelled.");
        }
        catch (Exception ex)
        {
            LogLine($"Error: {ex.Message}");
        }
        finally
        {
            TweaksList.ItemsSource = null;
            TweaksList.ItemsSource = tweaks;
            EndBusy();
        }
    }
}
