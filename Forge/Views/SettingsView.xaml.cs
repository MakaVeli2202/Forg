using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Forge.Views;

public partial class SettingsView : UserControl
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "Forge";

    private bool _loading = true;

    public SettingsView()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            VersionText.Text =
                $"Version {Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0"}";

            bool elevated = new WindowsPrincipal(WindowsIdentity.GetCurrent())
                .IsInRole(WindowsBuiltInRole.Administrator);

            ElevationText.Text = elevated ? "Running elevated" : "Not elevated";
            ElevationText.Foreground = elevated
                ? System.Windows.Media.Brushes.LightGreen
                : System.Windows.Media.Brushes.OrangeRed;

            _loading = true;
            StartupCheck.IsChecked = IsStartupEnabled();
            _loading = false;
        };
    }

    private static string ConfigPath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);

        return key?.GetValue(RunValueName) != null;
    }

    private void OpenConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ConfigPath,
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    private void ReloadCatalogs_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Catalogs reload automatically each time you open the Apps or Tweaks pages.",
            "Forge",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        Services.AppCacheService.Clear();

        MessageBox.Show(
            "App cache cleared. The next page load will perform a fresh scan.",
            "Forge",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void Startup_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);

            if (StartupCheck.IsChecked == true)
            {
                string exe = Environment.ProcessPath ?? string.Empty;

                if (!string.IsNullOrEmpty(exe))
                {
                    key.SetValue(RunValueName, $"\"{exe}\"");
                }
            }
            else
            {
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
        }
    }
}
