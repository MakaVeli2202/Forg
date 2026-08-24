using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Forge.Views;

public partial class IsoCreatorView : UserControl
{
    public IsoCreatorView()
    {
        InitializeComponent();
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
            IsoStatusText.Text = string.Empty;
        }
    }

    private void CreateIso_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(IsoPathBox.Text) ||
            !System.IO.File.Exists(IsoPathBox.Text))
        {
            IsoStatusText.Text = "Pick a valid ISO first (Step 1).";
            return;
        }

        IsoStatusText.Text = "Offline build pipeline coming online soon.";
    }
}
