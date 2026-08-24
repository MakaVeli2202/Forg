using Forge.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Forge
{
    public partial class MainWindow : Window
    {
        private readonly Brush DefaultBrush =
            new SolidColorBrush(Color.FromRgb(0x1C, 0x13, 0x0C));

        private readonly Brush SelectedBrush =
            new SolidColorBrush(Color.FromRgb(0xFF, 0x7A, 0x00));

        private readonly Brush DefaultNavForeground =
            new SolidColorBrush(Color.FromRgb(0xFF, 0xF3, 0xE8));

        private readonly Brush SelectedNavForeground =
            new SolidColorBrush(Color.FromRgb(0x1A, 0x0F, 0x05));

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
            BtnHome.Background = DefaultBrush;
            BtnApps.Background = DefaultBrush;
            BtnTweaks.Background = DefaultBrush;
            BtnSystem.Background = DefaultBrush;
            BtnUpdates.Background = DefaultBrush;
            BtnDrivers.Background = DefaultBrush;
            BtnIso.Background = DefaultBrush;
            BtnSettings.Background = DefaultBrush;

            BtnHome.Foreground = DefaultNavForeground;
            BtnApps.Foreground = DefaultNavForeground;
            BtnTweaks.Foreground = DefaultNavForeground;
            BtnSystem.Foreground = DefaultNavForeground;
            BtnUpdates.Foreground = DefaultNavForeground;
            BtnDrivers.Foreground = DefaultNavForeground;
            BtnIso.Foreground = DefaultNavForeground;
            BtnSettings.Foreground = DefaultNavForeground;
        }

        private void ActivateNav(Button button)
        {
            button.Background = SelectedBrush;
            button.Foreground = SelectedNavForeground;
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
                    MainContent.Content = new AppsView();
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

                case "iso":
                    ActivateNav(BtnIso);
                    PageTitle.Text = "ISO CREATOR";
                    MainContent.Content = new IsoCreatorView();
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

        private void ViewCounters_Checked(object sender, RoutedEventArgs e) =>
            CurrentAppsView?.SetSectionVisibility("StatsSection", (sender as System.Windows.Controls.MenuItem)?.IsChecked == true);

        private void ViewSearch_Checked(object sender, RoutedEventArgs e) =>
            CurrentAppsView?.SetSectionVisibility("SearchSection", (sender as System.Windows.Controls.MenuItem)?.IsChecked == true);

        private void ViewFilters_Checked(object sender, RoutedEventArgs e) =>
            CurrentAppsView?.SetSectionVisibility("FiltersSection", (sender as System.Windows.Controls.MenuItem)?.IsChecked == true);

        private void ViewSelection_Checked(object sender, RoutedEventArgs e) =>
            CurrentAppsView?.SetSectionVisibility("SelectionSection", (sender as System.Windows.Controls.MenuItem)?.IsChecked == true);

        private void ViewActivity_Checked(object sender, RoutedEventArgs e) =>
            CurrentAppsView?.SetSectionVisibility("ActivitySection", (sender as System.Windows.Controls.MenuItem)?.IsChecked == true);

        private void ViewStatus_Checked(object sender, RoutedEventArgs e) =>
            CurrentAppsView?.SetSectionVisibility("StatusSection", (sender as System.Windows.Controls.MenuItem)?.IsChecked == true);

        private void ViewActions_Checked(object sender, RoutedEventArgs e) =>
            CurrentAppsView?.SetSectionVisibility("ActionsSection", (sender as System.Windows.Controls.MenuItem)?.IsChecked == true);





    }
}