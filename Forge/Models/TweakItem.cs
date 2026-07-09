namespace Forge.Models;

public sealed class TweakItem
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsEnabled { get; set; }
}