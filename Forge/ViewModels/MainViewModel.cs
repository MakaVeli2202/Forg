using System.Collections.ObjectModel;
using Forge.Models;

namespace Forge.ViewModels;

public class MainViewModel : BaseViewModel
{
    private string _selectedSection = "Home";

    public MainViewModel()
    {
        NavigationItems = new ObservableCollection<string>
        {
            "Home",
            "Apps",
            "Tweaks",
            "System",
            "Updates"
        };

        Apps = new ObservableCollection<AppItem>();
        Tweaks = new ObservableCollection<TweakItem>();
    }

    public string Title => "Forge";

    public ObservableCollection<string> NavigationItems { get; }

    public ObservableCollection<AppItem> Apps { get; }

    public ObservableCollection<TweakItem> Tweaks { get; }

    public string SelectedSection
    {
        get => _selectedSection;
        set => SetProperty(ref _selectedSection, value);
    }
}