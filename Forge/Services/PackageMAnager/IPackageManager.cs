namespace Forge.Services.PackageManager;

public interface IPackageManager
{
    Task InstallAsync(string packageId, string? source = null);

    Task UninstallAsync(string packageId, string? source = null);

    Task UpgradeAsync(string packageId, string? source = null);
}