using System.Diagnostics;
using System.Management;
using System.IO;

namespace Forge.Services;

public static class SystemInfoHelper
{
    public static string GetWindowsVersion()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Caption, Version, BuildNumber FROM Win32_OperatingSystem");
            foreach (var obj in searcher.Get())
            {
                string caption = obj["Caption"]?.ToString() ?? "Windows";
                string version = obj["Version"]?.ToString() ?? "";
                string build = obj["BuildNumber"]?.ToString() ?? "";

                if (build.Length >= 5)
                {
                    int buildNum = int.Parse(build);
                    if (buildNum >= 22000)
                    {
                        if (!caption.Contains("Windows 11"))
                            caption = caption.Replace("Windows 10", "Windows 11");
                    }
                    else
                    {
                        if (!caption.Contains("Windows 10"))
                            caption = caption.Replace("Windows 11", "Windows 10");
                    }

                    string releaseId = buildNum switch
                    {
                        >= 26100 => "24H2",
                        >= 22631 => "23H2",
                        >= 22621 => "22H2",
                        >= 19045 => "22H2",
                        >= 19044 => "21H2",
                        _ => ""
                    };

                    if (!string.IsNullOrEmpty(releaseId) && !caption.Contains(releaseId))
                        caption += " " + releaseId;
                }

                return caption.Trim();
            }
        }
        catch
        {
        }

        return "Windows 11";
    }

    public static (string Name, int Cores) GetCpuInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                string name = obj["Name"]?.ToString() ?? "Unknown CPU";
                int cores = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                return (name.Trim(), cores);
            }
        }
        catch
        {
        }

        return ("Unknown CPU", 0);
    }

    public static (double TotalGB, int SpeedMHz) GetMemoryInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Capacity, Speed FROM Win32_PhysicalMemory");
            long totalBytes = 0;
            int speedMHz = 0;
            int count = 0;

            foreach (var obj in searcher.Get())
            {
                totalBytes += Convert.ToInt64(obj["Capacity"] ?? 0);
                speedMHz += Convert.ToInt32(obj["Speed"] ?? 0);
                count++;
            }

            double totalGB = totalBytes / (1024.0 * 1024 * 1024);
            int avgSpeed = count > 0 ? speedMHz / count : 0;

            return (Math.Round(totalGB, 1), avgSpeed);
        }
        catch
        {
        }

        return (0, 0);
    }

    public static string GetGpuInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterCompatibility FROM Win32_VideoController");
            string fallback = "";
            foreach (var obj in searcher.Get())
            {
                string name = obj["Name"]?.ToString() ?? "";
                string compat = obj["AdapterCompatibility"]?.ToString() ?? "";

                if (!compat.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) &&
                    !name.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
                {
                    return name.Trim();
                }

                if (string.IsNullOrEmpty(fallback))
                    fallback = name.Trim();
            }

            if (!string.IsNullOrEmpty(fallback))
                return fallback;
        }
        catch
        {
        }

        return "Unknown GPU";
    }

    public static (double TotalGB, double FreeGB) GetStorageInfo()
    {
        try
        {
            var drive = DriveInfo.GetDrives()
                .FirstOrDefault(d => d.Name.Equals(@"C:\", StringComparison.OrdinalIgnoreCase) ||
                                     d.Name.Equals("C:", StringComparison.OrdinalIgnoreCase));

            if (drive != null && drive.IsReady)
            {
                double totalGB = drive.TotalSize / (1024.0 * 1024 * 1024);
                double freeGB = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                return (Math.Round(totalGB, 1), Math.Round(freeGB, 1));
            }
        }
        catch
        {
        }

        return (0, 0);
    }
}
