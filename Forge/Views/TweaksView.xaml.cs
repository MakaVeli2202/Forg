using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Forge.Models;
using Forge.Services;

namespace Forge.Views;

public partial class TweaksView : UserControl
{
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
            new PropertyGroupDescription(nameof(TweakItem.Category)));

        _view.Filter = FilterTweak;

        SearchBox.TextChanged += SearchBox_TextChanged;
    }

    private bool? SafeState(TweakItem tweak)
    {
        try
        {
            return _tweakService.GetAppliedState(tweak);
        }
        catch
        {
            return null;
        }
    }

    private bool FilterTweak(object item)
    {
        string query = SearchBox.Text.Trim();

        if (query.Length == 0)
        {
            return true;
        }

        if (item is not TweakItem tweak)
        {
            return false;
        }

        return tweak.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               (tweak.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
               tweak.Category.Contains(query, StringComparison.OrdinalIgnoreCase);
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
        if (sender is not Button button ||
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

            case "AddUltPerf":
                await RunExclusiveAsync(
                    "Enabling Ultimate Performance plan...",
                    ct => _tweakService.SetUltimatePerformanceAsync(true, ct));
                break;

            case "RemoveUltPerf":
                await RunExclusiveAsync(
                    "Restoring default power plans...",
                    ct => _tweakService.SetUltimatePerformanceAsync(false, ct));
                break;

            default:
                await RunExclusiveAsync(
                    $"Running {tweak.Name}...",
                    ct => _tweakService.ApplyAsync(tweak, ct),
                    () => tweak.IsApplied = SafeState(tweak));
                break;
        }
    }

    private async void BtnApplySelected_Click(
        object sender,
        RoutedEventArgs e)
    {
        var selected = GetSelectedTweaks().ToList();

        if (selected.Count == 0)
        {
            SetStatus("Nothing selected.");
            return;
        }

        foreach (var tweak in selected)
        {
            await RunExclusiveAsync(
                $"Applying: {tweak.Name}",
                ct => _tweakService.ApplyAsync(tweak, ct),
                () => tweak.IsApplied = SafeState(tweak),
                keepBusyBetweenItems: true);
        }

        EndBusy();
        SetStatus($"Applied {selected.Count} tweak(s).");
    }

    private async void BtnUndoSelected_Click(
        object sender,
        RoutedEventArgs e)
    {
        var selected = GetSelectedTweaks().ToList();

        if (selected.Count == 0)
        {
            SetStatus("Nothing selected.");
            return;
        }

        foreach (var tweak in selected)
        {
            await RunExclusiveAsync(
                $"Undoing: {tweak.Name}",
                ct => _tweakService.UndoAsync(tweak, ct),
                () => tweak.IsApplied = SafeState(tweak),
                keepBusyBetweenItems: true);
        }

        EndBusy();
        SetStatus($"Undid {selected.Count} tweak(s).");
    }

    private IEnumerable<TweakItem> GetSelectedTweaks() =>
        (_tweakService.Tweaks).Where(t => t.IsCheckbox && t.IsSelected);

    private async Task RunExclusiveAsync(
        string statusMessage,
        Func<CancellationToken, Task> action,
        Action? onComplete = null,
        bool keepBusyBetweenItems = false)
    {
        BeginBusy(statusMessage, keepBusyBetweenItems);

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

            if (!keepBusyBetweenItems)
            {
                EndBusy();
            }
        }
    }

    private void BeginBusy(
        string message,
        bool alreadyBusy = false)
    {
        if (!alreadyBusy || !_isBusy)
        {
            _isBusy = true;
        }

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
        BtnApplySelected.IsEnabled = enabled;
        BtnUndoSelected.IsEnabled = enabled;
        BtnSelectAll.IsEnabled = enabled;
        BtnSelectNotApplied.IsEnabled = enabled;
        BtnClearSelection.IsEnabled = enabled;
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

    private void BtnSelectNotApplied_Click(
        object sender,
        RoutedEventArgs e)
    {
        foreach (var tweak in _tweakService.Tweaks.Where(t => t.IsCheckbox))
        {
            tweak.IsSelected = tweak.IsApplied != true;
        }
    }

    private void BtnSelectAll_Click(
        object sender,
        RoutedEventArgs e)
    {
        foreach (var tweak in _tweakService.Tweaks.Where(t => t.IsCheckbox))
        {
            tweak.IsSelected = true;
        }
    }

    private void BtnClearSelection_Click(
        object sender,
        RoutedEventArgs e)
    {
        foreach (var tweak in _tweakService.Tweaks.Where(t => t.IsCheckbox))
        {
            tweak.IsSelected = false;
        }
    }

    private void BtnCancel_Click(
        object sender,
        RoutedEventArgs e)
    {
        _tweakService.Cancel();
    }
}
