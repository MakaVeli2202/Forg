using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Forge.Views;

namespace Forge
{
    public partial class MainWindow : Window
    {
        private static readonly Brush NavMuted = new SolidColorBrush(Color.FromRgb(0x9A, 0x90, 0x88));
        private static readonly Brush NavActive = new SolidColorBrush(Color.FromRgb(0xFF, 0x7A, 0x00));

        private AppsView? CurrentAppsView => MainContent?.Content as AppsView;

        public MainWindow()
        {
            InitializeComponent();

            ActivateNav(BtnHome);

            PageTitle.Text = "Home".ToUpperInvariant();
            MainContent.Content = new HomeView();
        }

        private void ResetNavigation()
        {
            BtnHome.Tag = null;
            BtnApps.Tag = null;
            BtnTweaks.Tag = null;
            BtnSystem.Tag = null;
            BtnUpdates.Tag = null;
            BtnDrivers.Tag = null;
            BtnSettings.Tag = null;

            SetNavColors(IconHomePath, TextHome, false);
            SetNavColors(IconAppsPath, TextApps, false);
            SetNavColors(IconTweaksPath, TextTweaks, false);
            SetNavColors(IconSystemPath, TextSystem, false);
            SetNavColors(IconUpdatesPath, TextUpdates, false);
            SetNavColors(IconDriversPath, TextDrivers, false);
            SetNavColors(IconSettingsPath, TextSettings, false);
        }

        private void ActivateNav(Button button)
        {
            button.Tag = "active";

            if (button == BtnHome) SetNavColors(IconHomePath, TextHome, true);
            else if (button == BtnApps) SetNavColors(IconAppsPath, TextApps, true);
            else if (button == BtnTweaks) SetNavColors(IconTweaksPath, TextTweaks, true);
            else if (button == BtnSystem) SetNavColors(IconSystemPath, TextSystem, true);
            else if (button == BtnUpdates) SetNavColors(IconUpdatesPath, TextUpdates, true);
            else if (button == BtnDrivers) SetNavColors(IconDriversPath, TextDrivers, true);
            else if (button == BtnSettings) SetNavColors(IconSettingsPath, TextSettings, true);
        }

        private static void SetNavColors(Path icon, TextBlock text, bool active)
        {
            Brush color = active ? NavActive : NavMuted;
            icon.Stroke = color;
            text.Foreground = color;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public void NavigateTo(string page)
        {
            ResetNavigation();

            switch ((page ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "home":
                    ActivateNav(BtnHome);
                    PageTitle.Text = "HOME";
                    MainContent.Content = new HomeView();
                    break;

                case "apps":
                    ActivateNav(BtnApps);
                    PageTitle.Text = "APPS";
                    var appsView = new AppsView();
                    MainContent.Content = appsView;
                    ApplySectionStates(appsView);
                    break;

                case "tweaks":
                    ActivateNav(BtnTweaks);
                    PageTitle.Text = "TWEAKS";
                    MainContent.Content = new TweaksView();
                    break;

                case "system":
                    ActivateNav(BtnSystem);
                    PageTitle.Text = "SYSTEM";
                    MainContent.Content = new SystemView();
                    break;

                case "updates":
                    ActivateNav(BtnUpdates);
                    PageTitle.Text = "UPDATES";
                    MainContent.Content = new UpdatesView();
                    break;

                case "drivers":
                    ActivateNav(BtnDrivers);
                    PageTitle.Text = "DRIVERS";
                    MainContent.Content = new DriversView();
                    break;

                case "settings":
                    ActivateNav(BtnSettings);
                    PageTitle.Text = "SETTINGS";
                    MainContent.Content = new SettingsView();
                    break;
            }
        }

        private void BtnHome_Click(object sender, RoutedEventArgs e) => NavigateTo("home");
        private void BtnApps_Click(object sender, RoutedEventArgs e) => NavigateTo("apps");
        private void BtnTweaks_Click(object sender, RoutedEventArgs e) => NavigateTo("tweaks");
        private void BtnSystem_Click(object sender, RoutedEventArgs e) => NavigateTo("system");
        private void BtnUpdates_Click(object sender, RoutedEventArgs e) => NavigateTo("updates");
        private void BtnDrivers_Click(object sender, RoutedEventArgs e) => NavigateTo("drivers");
        private void BtnIso_Click(object sender, RoutedEventArgs e) => NavigateTo("iso");
        private void BtnSettings_Click(object sender, RoutedEventArgs e) => NavigateTo("settings");

        private static readonly Dictionary<string, bool> SectionStates = new()
        {
            ["StatsSection"] = true,
            ["SearchSection"] = true,
            ["FiltersSection"] = true,
            ["SelectionSection"] = true,
            ["ActivitySection"] = true,
            ["StatusSection"] = true,
            ["ActionsSection"] = true
        };

        private void SetSectionState(string sectionName, object sender, RoutedEventArgs e)
        {
            bool visible = (sender as System.Windows.Controls.MenuItem)?.IsChecked == true;
            SectionStates[sectionName] = visible;
            CurrentAppsView?.SetSectionVisibility(sectionName, visible);
        }

        private void ApplySectionStates(AppsView appsView)
        {
            foreach (var pair in SectionStates)
                appsView.SetSectionVisibility(pair.Key, pair.Value);
        }

        private void ViewCounters_Checked(object sender, RoutedEventArgs e) => SetSectionState("StatsSection", sender, e);
        private void ViewSearch_Checked(object sender, RoutedEventArgs e) => SetSectionState("SearchSection", sender, e);
        private void ViewFilters_Checked(object sender, RoutedEventArgs e) => SetSectionState("FiltersSection", sender, e);
        private void ViewSelection_Checked(object sender, RoutedEventArgs e) => SetSectionState("SelectionSection", sender, e);
        private void ViewActivity_Checked(object sender, RoutedEventArgs e) => SetSectionState("ActivitySection", sender, e);
        private void ViewStatus_Checked(object sender, RoutedEventArgs e) => SetSectionState("StatusSection", sender, e);
        private void ViewActions_Checked(object sender, RoutedEventArgs e) => SetSectionState("ActionsSection", sender, e);
    }
}
