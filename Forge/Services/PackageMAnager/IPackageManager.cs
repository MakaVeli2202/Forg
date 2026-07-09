namespace Forge.Services.PackageManager;

public interface IPackageManager
{
    Task InstallAsync(string packageId);

    Task UninstallAsync(string packageId);

    Task UpgradeAsync(string packageId);
}