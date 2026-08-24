using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Forge.Models;

public sealed class TweakItem : INotifyPropertyChanged
{
    private bool? _isApplied;
    private bool _isSelected;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "checkbox";

    [JsonPropertyName("comboItems")]
    public List<string>? ComboItems { get; set; }

    [JsonPropertyName("registry")]
    public List<TweakRegistryEntry>? Registry { get; set; }

    [JsonPropertyName("services")]
    public List<TweakServiceEntry>? Services { get; set; }

    [JsonPropertyName("invokeScript")]
    public string? InvokeScript { get; set; }

    [JsonPropertyName("undoScript")]
    public string? UndoScript { get; set; }

    [JsonIgnore]
    public bool IsCheckbox => Type is "checkbox" or "toggle";

    [JsonIgnore]
    public bool IsCombobox => Type == "combobox";

    [JsonIgnore]
    public bool IsButton => Type == "button";

    /// <summary>
    /// True when the tweak is currently applied on this system
    /// (mirrors WinUtil's Get-WinUtilToggleStatus).
    /// </summary>
    [JsonIgnore]
    public bool? IsApplied
    {
        get => _isApplied;
        set
        {
            if (_isApplied == value)
            {
                return;
            }

            _isApplied = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StateDisplay));
        }
    }

    /// <summary>
    /// User selection for batch apply/undo.
    /// </summary>
    [JsonIgnore]
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

    [JsonIgnore]
    public string StateDisplay =>
        _isApplied switch
        {
            true => "Applied",
            false => "Not Applied",
            _ => "Unknown"
        };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class TweakRegistryEntry
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("originalValue")]
    public string? OriginalValue { get; set; }

    [JsonPropertyName("defaultState")]
    public string? DefaultState { get; set; }

    [JsonPropertyName("values")]
    public Dictionary<string, string>? Values { get; set; }

    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; set; }
}

public sealed class TweakServiceEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("startupType")]
    public string StartupType { get; set; } = string.Empty;

    [JsonPropertyName("originalType")]
    public string OriginalType { get; set; } = string.Empty;
}
