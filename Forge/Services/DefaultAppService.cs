using Microsoft.Win32;
using System.Diagnostics;
using System.IO;

namespace Forge.Services;

public static class DefaultAppService
{
    public static string ApplyDefaults(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return string.Empty;
        }

        return action.ToLowerInvariant() switch
        {
            "setasdefaultbrowser" => SetDefaultBrowser(),
            "setasdefaultmediaplayer" => SetDefaultMediaPlayer(),
            "setasdefaulttexteditor" => SetDefaultTextEditor(),
            "setasdefaultarchiver" => SetDefaultArchiver(),
            _ => string.Empty
        };
    }

    private static string SetDefaultBrowser()
    {
        const string progId = "ChromeHTML";
        const string exePath = @"Google\Chrome\Application\chrome.exe";

        string? chromePath = FindExecutable(exePath);

        if (chromePath is null)
        {
            return "Chrome executable not found; skipped default browser setup.";
        }

        RegisterProgId(progId, chromePath, "Chrome HTML Document");
        AssociateExtensions([".htm", ".html"], progId);
        SetProtocolHandler("http", progId, chromePath);
        SetProtocolHandler("https", progId, chromePath);

        return "Set Chrome as default browser.";
    }

    private static string SetDefaultMediaPlayer()
    {
        const string progId = "VLC.vlc";
        const string exePath = @"VideoLAN\VLC\vlc.exe";

        string? vlcPath = FindExecutable(exePath);

        if (vlcPath is null)
        {
            return "VLC executable not found; skipped default media player setup.";
        }

        RegisterProgId(progId, vlcPath, "VLC Media File");

        string[] videoExtensions =
        [
            ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm",
            ".mpg", ".mpeg", ".m4v", ".3gp", ".ts", ".vob", ".ogv"
        ];

        string[] audioExtensions =
        [
            ".mp3", ".flac", ".wav", ".ogg", ".aac", ".wma", ".m4a",
            ".opus", ".aiff", ".ape", ".wv"
        ];

        AssociateExtensions(videoExtensions, progId);
        AssociateExtensions(audioExtensions, progId);

        return "Set VLC as default media player.";
    }

    private static string SetDefaultTextEditor()
    {
        const string progId = "Notepad++.Text";
        const string exePath = @"Notepad++\notepad++.exe";

        string? nppPath = FindExecutable(exePath);

        if (nppPath is null)
        {
            return "Notepad++ executable not found; skipped default text editor setup.";
        }

        RegisterProgId(progId, nppPath, "Notepad++ Text File");

        string[] textExtensions =
        [
            ".txt", ".log", ".ini", ".cfg", ".conf", ".config",
            ".json", ".xml", ".csv", ".tsv", ".yaml", ".yml",
            ".md", ".markdown", ".rst",
            ".cs", ".py", ".js", ".ts", ".jsx", ".tsx",
            ".html", ".css", ".scss", ".less",
            ".sql", ".sh", ".bat", ".cmd", ".ps1",
            ".java", ".c", ".cpp", ".h", ".hpp",
            ".rb", ".php", ".go", ".rs", ".swift",
            ".bat", ".reg", ".inf", ".nfo"
        ];

        AssociateExtensions(textExtensions, progId);

        return "Set Notepad++ as default text editor.";
    }

    private static string SetDefaultArchiver()
    {
        const string progId = "WinRAR.Archive";
        const string exePath = @"WinRAR\WinRAR.exe";

        string? rarPath = FindExecutable(exePath);

        if (rarPath is null)
        {
            return "WinRAR executable not found; skipped default archiver setup.";
        }

        RegisterProgId(progId, rarPath, "WinRAR Archive");

        string[] archiveExtensions =
        [
            ".rar", ".zip", ".7z", ".tar", ".gz", ".bz2",
            ".xz", ".tgz", ".tbz2", ".tar.gz", ".tar.bz2",
            ".cab", ".iso", ".jar", ".war", ".ear"
        ];

        AssociateExtensions(archiveExtensions, progId);

        return "Set WinRAR as default archiver.";
    }

    private static string? FindExecutable(string relativePath)
    {
        string[] searchPaths =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        ];

        foreach (string basePath in searchPaths)
        {
            if (string.IsNullOrEmpty(basePath))
            {
                continue;
            }

            string fullPath = Path.Combine(basePath, relativePath);

            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private static void RegisterProgId(
        string progId,
        string executablePath,
        string description)
    {
        try
        {
            using var classesKey = Registry.CurrentUser.CreateSubKey(
                $@"Software\Classes\{progId}");

            classesKey.SetValue(string.Empty, description);

            using var defaultIcon = classesKey.CreateSubKey("DefaultIcon");
            defaultIcon.SetValue(string.Empty, $"\"{executablePath}\",0");

            using var shell = classesKey.CreateSubKey("shell");
            using var open = shell.CreateSubKey("open");
            using var command = open.CreateSubKey("command");
            command.SetValue(string.Empty, $"\"{executablePath}\" \"%1\"");
        }
        catch
        {
            // Registry write failed silently
        }
    }

    private static void AssociateExtensions(
        string[] extensions,
        string progId)
    {
        foreach (string ext in extensions)
        {
            try
            {
                using var extKey = Registry.CurrentUser.CreateSubKey(
                    $@"Software\Classes\{ext}");

                extKey.SetValue(string.Empty, progId);

                using var openWith = extKey.CreateSubKey("OpenWithProgids");
                openWith.SetValue(progId, Array.Empty<byte>());
            }
            catch
            {
                // Continue with remaining extensions
            }
        }
    }

    private static void SetProtocolHandler(
        string protocol,
        string progId,
        string executablePath)
    {
        try
        {
            using var protocolKey = Registry.CurrentUser.CreateSubKey(
                $@"Software\Classes\{protocol}");

            protocolKey.SetValue(string.Empty, $"URL:{progId} Protocol");
            protocolKey.SetValue("URL Protocol", string.Empty);

            using var defaultIcon = protocolKey.CreateSubKey("DefaultIcon");
            defaultIcon.SetValue(string.Empty, $"\"{executablePath}\",0");

            using var shell = protocolKey.CreateSubKey("shell");
            using var open = shell.CreateSubKey("open");
            using var command = open.CreateSubKey("command");
            command.SetValue(string.Empty, $"\"{executablePath}\" \"%1\"");
        }
        catch
        {
            // Protocol registration failed silently
        }
    }
}
