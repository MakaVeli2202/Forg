using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Forge.Views;

public partial class SystemView : UserControl
{
    public SystemView()
    {
        InitializeComponent();
    }

    private void OpenTool_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string tool ||
            string.IsNullOrWhiteSpace(tool))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = tool,
                UseShellExecute = true
            });
        }
        catch
        {
            MessageBox.Show(
                $"Could not launch '{tool}'.",
                "Forge",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
