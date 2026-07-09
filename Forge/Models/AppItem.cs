using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Forge.Models;

public class AppItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isInstalled;
    private AppStatus _status = AppStatus.Available;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("wingetId")]
    public string WingetId { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("recommended")]
    public bool Recommended { get; set; }

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonPropertyName("publisher")]
    public string Publisher { get; set; } = string.Empty;

    [JsonPropertyName("homepage")]
    public string Homepage { get; set; } = string.Empty;

    [JsonPropertyName("featured")]
    public bool Featured { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];


    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;

            OnPropertyChanged();
        }
    }

    public bool IsInstalled
    {
        get => _isInstalled;
        set
        {
            if (_isInstalled == value)
            {
                return;
            }

            _isInstalled = value;

            OnPropertyChanged();
        }
    }

    public AppStatus Status
    {
        get => _status;
        set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusDisplay));
        }
    }

    public string StatusDisplay =>
    Status switch
    {
        AppStatus.Installed => "Installed",
        AppStatus.Installing => "Installing",
        AppStatus.Upgrading => "Upgrading",
        AppStatus.Uninstalling => "Uninstalling",
        AppStatus.Cancelling => "Cancelling",
        AppStatus.Failed => "Failed",
        _ => "Available"
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}