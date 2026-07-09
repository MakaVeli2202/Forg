using Forge.Models;

namespace Forge.Services;

public class TweakService
{
    public Task<IReadOnlyList<TweakItem>> GetTweaksAsync()
    {
        IReadOnlyList<TweakItem> tweaks = Array.Empty<TweakItem>();
        return Task.FromResult(tweaks);
    }

    public Task ApplyTweakAsync(TweakItem tweak)
    {
        return Task.CompletedTask;
    }
}