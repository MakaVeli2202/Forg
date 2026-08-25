using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Forge.Services;

namespace Forge.Views;

public partial class IsoCreatorView : UserControl
{
    private readonly IsoCreatorService _isoService;
    private CancellationTokenSource? _buildCts;
    private bool _isBuilding;

    public IsoCreatorView()
    {
        InitializeComponent();
        _isoService = new IsoCreatorService(AppendLog);
        DetectTools();
    }

    private void DetectTools()
    {
        _isoService.DetectTools();
        DismIndicator.Fill = _isoService.HasDism
            ? new SolidColorBrush(Color.FromRgb(0x7A, 0xFF, 0x50))
            : new SolidColorBrush(Color.FromRgb(0xFF, 0x50, 0x50));
        OscdimgIndicator.Fill = _isoService.HasOscdimg
            ? new SolidColorBrush(Color.FromRgb(0x7A, 0xFF, 0x50))
            : new SolidColorBrush(Color.FromRgb(0xFF, 0x50, 0x50));

        if (!_isoService.HasDism || !_isoService.HasOscdimg)
        {
            BtnCreateIso.IsEnabled = false;
            var missing = new List<string>();
            if (!_isoService.HasDism) missing.Add("DISM");
            if (!_isoService.HasOscdimg) missing.Add("oscdimg");
            StatusText.Visibility = Visibility.Visible;
            StatusText.Text = $"Missing tools: {string.Join(", ", missing)}. Install Windows ADK or bundle oscdimg.";
        }
    }

    private void DownloadIso_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.microsoft.com/software-download/windows11",
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    private void BrowseIso_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select a Windows ISO",
            Filter = "ISO images (*.iso)|*.iso|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            IsoPathBox.Text = dialog.FileName;
            StatusText.Visibility = Visibility.Collapsed;
            _ = LoadEditionsAsync(dialog.FileName);
        }
    }

    private async Task LoadEditionsAsync(string isoPath)
    {
        if (!_isoService.HasDism) return;

        EditionCombo.IsEnabled = false;
        EditionCombo.Items.Clear();
        StatusText.Visibility = Visibility.Visible;
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x90, 0x88));
        StatusText.Text = "Reading editions from ISO...";

        try
        {
            var editions = await _isoService.GetEditionsAsync(isoPath);
            foreach (var edition in editions)
            {
                EditionCombo.Items.Add(edition);
            }

            if (EditionCombo.Items.Count > 0)
            {
                EditionCombo.SelectedIndex = 0;
                EditionCombo.IsEnabled = true;
                StatusText.Visibility = Visibility.Collapsed;
            }
            else
            {
                StatusText.Text = "No editions found in this ISO.";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x50, 0x50));
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Failed to read editions: {ex.Message}";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x50, 0x50));
        }
    }

    private void BrowseOutputFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select output folder for the ISO"
        };

        if (dialog.ShowDialog() == true)
        {
            OutputFolderBox.Text = dialog.FolderName;
        }
    }

    private async void CreateIso_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(IsoPathBox.Text) || !File.Exists(IsoPathBox.Text))
        {
            StatusText.Visibility = Visibility.Visible;
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x50, 0x50));
            StatusText.Text = "Pick a valid ISO first (Step 1).";
            return;
        }

        if (EditionCombo.SelectedItem is not IsoEditionInfo edition)
        {
            StatusText.Visibility = Visibility.Visible;
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x50, 0x50));
            StatusText.Text = "Select an edition (Step 2).";
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputFolderBox.Text))
        {
            StatusText.Visibility = Visibility.Visible;
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x50, 0x50));
            StatusText.Text = "Choose an output folder (Step 2).";
            return;
        }

        var outputName = OutputNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(outputName))
            outputName = "Forge-Win11";

        SetBuilding(true);
        _buildCts = new CancellationTokenSource();
        LogText.Text = "";
        AppendLog($"Starting build — edition: {edition.Name} (index {edition.Index})");

        try
        {
            var result = await _isoService.BuildIsoAsync(
                IsoPathBox.Text,
                OutputFolderBox.Text,
                outputName,
                edition.Index,
                msg =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        StatusText.Text = msg;
                        StatusText.Visibility = Visibility.Visible;
                    });
                },
                _buildCts.Token);

            if (result.Success)
            {
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x7A, 0xFF, 0x50));
                StatusText.Text = result.Message;
            }
            else
            {
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x50, 0x50));
                StatusText.Text = $"Build failed: {result.Message}";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x90, 0x88));
            StatusText.Text = "Build cancelled.";
            AppendLog("Build cancelled by user.");
        }
        catch (Exception ex)
        {
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x50, 0x50));
            StatusText.Text = $"Unexpected error: {ex.Message}";
            AppendLog($"ERROR: {ex}");
        }
        finally
        {
            SetBuilding(false);
            _buildCts?.Dispose();
            _buildCts = null;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _buildCts?.Cancel();
    }

    private void SetBuilding(bool building)
    {
        _isBuilding = building;
        BtnCreateIso.IsEnabled = !building && _isoService.HasDism && _isoService.HasOscdimg;
        BtnCancel.Visibility = building ? Visibility.Visible : Visibility.Collapsed;
        BuildProgress.Visibility = building ? Visibility.Visible : Visibility.Collapsed;
        if (building) BuildProgress.IsIndeterminate = true;
        else { BuildProgress.IsIndeterminate = false; BuildProgress.Value = 0; }
    }

    private void AppendLog(string msg)
    {
        Dispatcher.Invoke(() =>
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogText.Text += $"[{timestamp}] {msg}\n";
            LogScroller.ScrollToEnd();
        });
    }
}
