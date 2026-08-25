using System.IO;
using System.Text.Json;

namespace Forge.Services;

public static class AppCacheService
{
    private static readonly string CacheDir =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Forge");

    private static readonly string CacheFile =
        Path.Combine(CacheDir, "installed_cache.json");

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private static CachedData? _memoryCache;

    public static bool IsStale()
    {
        if (_memoryCache is not null)
        {
            return DateTime.UtcNow - _memoryCache.Timestamp > CacheDuration;
        }

        if (!File.Exists(CacheFile))
        {
            return true;
        }

        try
        {
            string json = File.ReadAllText(CacheFile);
            _memoryCache = JsonSerializer.Deserialize<CachedData>(json);

            if (_memoryCache is null)
            {
                return true;
            }

            return DateTime.UtcNow - _memoryCache.Timestamp > CacheDuration;
        }
        catch
        {
            return true;
        }
    }

    public static List<string>? GetInstalledIds()
    {
        if (_memoryCache is not null && !IsStale())
        {
            return _memoryCache.InstalledIds;
        }

        if (!File.Exists(CacheFile))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(CacheFile);
            _memoryCache = JsonSerializer.Deserialize<CachedData>(json);

            if (_memoryCache is not null && !IsStale())
            {
                return _memoryCache.InstalledIds;
            }
        }
        catch
        {
            // Corrupted cache, treat as stale
        }

        return null;
    }

    public static void SetInstalledIds(List<string> ids)
    {
        _memoryCache = new CachedData
        {
            Timestamp = DateTime.UtcNow,
            InstalledIds = ids
        };

        try
        {
            Directory.CreateDirectory(CacheDir);

            string json = JsonSerializer.Serialize(
                _memoryCache,
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(CacheFile, json);
        }
        catch
        {
            // Cache write failed silently
        }
    }

    public static void Clear()
    {
        _memoryCache = null;

        try
        {
            if (File.Exists(CacheFile))
            {
                File.Delete(CacheFile);
            }
        }
        catch
        {
            // Delete failed silently
        }
    }

    private class CachedData
    {
        public DateTime Timestamp { get; set; }
        public List<string> InstalledIds { get; set; } = [];
    }
}
