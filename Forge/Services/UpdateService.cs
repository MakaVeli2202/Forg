namespace Forge.Services;

public class UpdateService
{
    public Task<bool> CheckForUpdatesAsync()
    {
        return Task.FromResult(false);
    }

    public Task ApplyUpdatesAsync()
    {
        return Task.CompletedTask;
    }
}