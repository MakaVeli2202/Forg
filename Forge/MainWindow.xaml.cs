using Forge.Views;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Forge
{
    public partial class MainWindow : Window
    {
        private readonly Brush DefaultBrush =
            new SolidColorBrush(Color.FromRgb(45, 45, 48));

        private readonly Brush SelectedBrush =
            new SolidColorBrush(Color.FromRgb(255, 122, 0));

        private AppsView? CurrentAppsView => MainContent.Content as AppsView;

        public MainWindow()
        {
            InitializeComponent();

            BtnHome.Background = SelectedBrush;

            PageTitle.Text = "Home";
            MainContent.Content = new HomeView();
        }

        private void ResetNavigation()
        {
            BtnHome.Background = DefaultBrush;
            BtnApps.Background = DefaultBrush;
            BtnTweaks.Background = DefaultBrush;
            BtnSystem.Background = DefaultBrush;
            BtnUpdates.Background = DefaultBrush;
            BtnIso.Background = DefaultBrush;
            BtnSettings.Background = DefaultBrush;
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

        private void BtnHome_Click(object sender, RoutedEventArgs e)
        {
            ResetNavigation();

            BtnHome.Background = SelectedBrush;

            PageTitle.Text = "Home";
            MainContent.Content = new HomeView();
        }

        private void BtnApps_Click(object sender, RoutedEventArgs e)
        {
            ResetNavigation();

            BtnApps.Background = SelectedBrush;

            PageTitle.Text = "Apps";
            MainContent.Content = new AppsView();
        }

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

        private void BtnTweaks_Click(object sender, RoutedEventArgs e)
        {
            ResetNavigation();

            BtnTweaks.Background = SelectedBrush;

            PageTitle.Text = "Tweaks";
            MainContent.Content = new TweaksView();
        }

        private void BtnSystem_Click(object sender, RoutedEventArgs e)
        {
            ResetNavigation();

            BtnSystem.Background = SelectedBrush;

            PageTitle.Text = "System";
            MainContent.Content = new SystemView();
        }

        private void BtnUpdates_Click(object sender, RoutedEventArgs e)
        {
            ResetNavigation();

            BtnUpdates.Background = SelectedBrush;

            PageTitle.Text = "Updates";
            MainContent.Content = new UpdatesView();
        }

        private void BtnIso_Click(object sender, RoutedEventArgs e)
        {
            ResetNavigation();

            BtnIso.Background = SelectedBrush;

            PageTitle.Text = "ISO Creator";
            MainContent.Content = new IsoCreatorView();
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            ResetNavigation();

            BtnSettings.Background = SelectedBrush;

            PageTitle.Text = "Settings";
            MainContent.Content = new SettingsView();
        }
    }
}