using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using ECommons.Configuration;
using EnhancedQuickPanel.Models;

namespace EnhancedQuickPanel.Services.CustomIcons;

/// <summary>Loads, caches, and provides textures for user-imported custom icons.</summary>
internal static class CustomIconRegistry
{
    private const string ManifestFileName = "custom-icons.json";
    private const string IconFolderName = "icon";
    private const int MaxDownloadBytes = 5 * 1024 * 1024;

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    private static readonly ConcurrentDictionary<string, ISharedImmediateTexture> TextureCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object ManifestLock = new();

    private static CustomIconManifest _manifest = new();
    private static string _iconDirectory = string.Empty;
    private static string _manifestPath = string.Empty;
    private static IReadOnlyList<CustomIconEntry> _sortedIcons = [];

    public static string IconDirectory => _iconDirectory;

    public static IReadOnlyList<CustomIconEntry> SortedIcons => _sortedIcons;

    public static void Initialize()
    {
        _iconDirectory = Path.Combine(EzConfig.GetPluginConfigDirectory(), IconFolderName);
        _manifestPath = Path.Combine(EzConfig.GetPluginConfigDirectory(), ManifestFileName);
        Directory.CreateDirectory(_iconDirectory);
        LoadManifest();
        RefreshSortedIcons();
    }

    public static void Dispose()
    {
        TextureCache.Clear();
    }

    public static uint ToIconId(CustomIconEntry entry) =>
        CustomIconIds.FromFileStem(CustomIconFileNames.GetStem(entry.FileName));

    public static string FormatDisplayId(CustomIconEntry entry) =>
        CustomIconIds.FormatDisplayId(entry.FileName);

    public static string FormatDisplayId(uint iconId)
    {
        if (!TryGetEntry(iconId, out var entry))
            return iconId.ToString();

        return FormatDisplayId(entry);
    }

    public static bool TryParseIconId(string? text, out uint iconId)
    {
        iconId = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        if (trimmed.StartsWith(CustomIconIds.DisplayPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var stem = trimmed[CustomIconIds.DisplayPrefix.Length..].Trim();
            return TryGetIconIdByFileStem(stem, out iconId);
        }

        return uint.TryParse(trimmed, out iconId) && CustomIconIds.IsCustom(iconId);
    }

    public static bool TryGetIconIdByFileStem(string fileStem, out uint iconId)
    {
        iconId = 0;
        if (!CustomIconFileNames.IsValidStem(fileStem))
            return false;

        lock (ManifestLock)
        {
            var entry = _manifest.Icons.FirstOrDefault(icon =>
                string.Equals(CustomIconFileNames.GetStem(icon.FileName), fileStem, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
                return false;

            iconId = ToIconId(entry);
            return iconId != 0;
        }
    }

    public static bool TryGetEntry(uint iconId, out CustomIconEntry entry)
    {
        entry = null!;
        if (!CustomIconIds.IsCustom(iconId))
            return false;

        lock (ManifestLock)
        {
            var found = _manifest.Icons.FirstOrDefault(icon => ToIconId(icon) == iconId);
            if (found == null)
                return false;

            entry = found;
            return true;
        }
    }

    public static bool TryGetTexture(uint iconId, out IDalamudTextureWrap texture)
    {
        texture = null!;
        if (!TryGetEntry(iconId, out var entry))
            return false;

        var path = GetIconPath(entry);
        if (!File.Exists(path))
            return false;

        try
        {
            var shared = TextureCache.GetOrAdd(path, static filePath => Svc.Texture.GetFromFile(filePath));
            var wrap = shared.GetWrapOrDefault();
            if (wrap == null || wrap.Handle == 0)
                return false;

            texture = wrap;
            return true;
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"[EQP] Custom icon texture load failed ({path}): {ex.Message}");
            return false;
        }
    }

    public static string? GetName(uint iconId) =>
        TryGetEntry(iconId, out var entry) ? entry.Name : null;

    public static void OpenIconFolder()
    {
        Directory.CreateDirectory(_iconDirectory);
        GenericHelpers.ShellStart(_iconDirectory);
    }

    public static void RefreshFromDisk()
    {
        Directory.CreateDirectory(_iconDirectory);
        TextureCache.Clear();
        LoadManifest();
        SyncManifestWithFolder();
        RefreshSortedIcons();
    }

    public static async Task<(bool Success, string Message)> DownloadAndSaveAsync(string url, string name)
    {
        if (string.IsNullOrWhiteSpace(url))
            return (false, T("customIcon.error.emptyUrl"));

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not "http" and not "https")
            return (false, T("customIcon.error.invalidUrl"));

        if (string.IsNullOrWhiteSpace(name))
            return (false, T("customIcon.error.emptyName"));

        try
        {
            using var response = await HttpClient.GetAsync(uri).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return (false, T("customIcon.error.downloadStatus", (int)response.StatusCode));

            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            if (bytes.Length == 0)
                return (false, T("customIcon.error.emptyImage"));

            if (bytes.Length > MaxDownloadBytes)
                return (false, T("customIcon.error.imageTooLarge"));

            if (!TryDetectImageExtension(bytes, out var extension))
                return (false, T("customIcon.error.unsupportedFormat"));

            CustomIconEntry entry;
            string filePath;
            lock (ManifestLock)
            {
                entry = CreateEntryLocked(name.Trim(), extension);
                filePath = GetIconPath(entry);
            }

            await File.WriteAllBytesAsync(filePath, bytes).ConfigureAwait(false);

            lock (ManifestLock)
                SaveManifestLocked();

            RefreshSortedIcons();
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"[EQP] Custom icon download failed: {ex.Message}");
            return (false, T("customIcon.error.downloadFailed", ex.Message));
        }
    }

    private static void SyncManifestWithFolder()
    {
        lock (ManifestLock)
        {
            _manifest.Icons.RemoveAll(entry => !IsManifestEntryValid(entry));
            ImportUnregisteredIconFilesLocked();
            SaveManifestLocked();
        }
    }

    private static bool IsManifestEntryValid(CustomIconEntry entry) =>
        CustomIconFileNames.IsImportableFileName(entry.FileName)
        && File.Exists(GetIconPath(entry));

    private static void ImportUnregisteredIconFilesLocked()
    {
        var knownFiles = _manifest.Icons
            .Select(entry => entry.FileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var originalFilePath in Directory.EnumerateFiles(_iconDirectory))
        {
            var filePath = originalFilePath;
            var fileName = Path.GetFileName(filePath);
            if (knownFiles.Contains(fileName))
                continue;

            if (!CustomIconFileNames.IsSupportedImageExtension(Path.GetExtension(fileName)))
                continue;

            var normalizedFileName = CustomIconFileNames.NormalizeFileName(fileName);
            if (string.IsNullOrWhiteSpace(CustomIconFileNames.GetStem(normalizedFileName)))
                continue;

            if (!string.Equals(fileName, normalizedFileName, StringComparison.Ordinal))
            {
                var normalizedPath = Path.Combine(_iconDirectory, normalizedFileName);
                if (File.Exists(normalizedPath)
                    && !string.Equals(normalizedPath, filePath, StringComparison.OrdinalIgnoreCase))
                    continue;

                File.Move(filePath, normalizedPath, overwrite: true);
                filePath = normalizedPath;
                fileName = normalizedFileName;
            }

            if (!CustomIconFileNames.IsImportableFileName(fileName))
                continue;

            var stem = CustomIconFileNames.GetStem(fileName);
            if (_manifest.Icons.Any(entry =>
                    string.Equals(CustomIconFileNames.GetStem(entry.FileName), stem, StringComparison.OrdinalIgnoreCase)))
                continue;

            _manifest.Icons.Add(new CustomIconEntry
            {
                Name = stem,
                FileName = fileName,
            });
            knownFiles.Add(fileName);
        }
    }

    private static CustomIconEntry CreateEntryLocked(string displayName, string extension)
    {
        var fileStem = CreateUniqueFileStem(displayName);
        var entry = new CustomIconEntry
        {
            Name = displayName,
            FileName = $"{fileStem}{extension}",
        };

        _manifest.Icons.Add(entry);
        return entry;
    }

    private static string CreateUniqueFileStem(string displayName)
    {
        var baseStem = CustomIconFileNames.NormalizeStemForNewFile(displayName);

        var stem = baseStem;
        var counter = 1;
        while (StemExistsLocked(stem))
        {
            stem = $"{baseStem}{counter}";
            counter++;
        }

        return stem;
    }

    private static bool StemExistsLocked(string stem) =>
        _manifest.Icons.Any(entry =>
            string.Equals(CustomIconFileNames.GetStem(entry.FileName), stem, StringComparison.OrdinalIgnoreCase))
        || Directory.EnumerateFiles(_iconDirectory)
            .Any(filePath => string.Equals(CustomIconFileNames.GetStem(filePath), stem, StringComparison.OrdinalIgnoreCase));

    private static string GetIconPath(CustomIconEntry entry) =>
        Path.Combine(_iconDirectory, entry.FileName);

    private static void LoadManifest()
    {
        lock (ManifestLock)
        {
            if (!File.Exists(_manifestPath))
            {
                _manifest = new CustomIconManifest();
                SaveManifestLocked();
                return;
            }

            try
            {
                var json = File.ReadAllText(_manifestPath, Encoding.UTF8);
                _manifest = JsonSerializer.Deserialize<CustomIconManifest>(json) ?? new CustomIconManifest();
                _manifest.Icons ??= [];
                _manifest.Icons.RemoveAll(entry => !IsManifestEntryValid(entry));
            }
            catch (Exception ex)
            {
                PluginLog.Debug($"[EQP] Custom icon manifest load failed: {ex.Message}");
                _manifest = new CustomIconManifest();
            }
        }
    }

    private static void SaveManifestLocked()
    {
        var json = JsonSerializer.Serialize(_manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_manifestPath, json, Encoding.UTF8);
    }

    private static void RefreshSortedIcons()
    {
        lock (ManifestLock)
        {
            _sortedIcons = _manifest.Icons
                .OrderBy(icon => icon.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(icon => icon.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    private static bool TryDetectImageExtension(ReadOnlySpan<byte> bytes, out string extension)
    {
        extension = string.Empty;
        if (bytes.Length >= 8
            && bytes[0] == 0x89
            && bytes[1] == 0x50
            && bytes[2] == 0x4E
            && bytes[3] == 0x47)
        {
            extension = ".png";
            return true;
        }

        if (bytes.Length >= 3
            && bytes[0] == 0xFF
            && bytes[1] == 0xD8
            && bytes[2] == 0xFF)
        {
            extension = ".jpg";
            return true;
        }

        if (bytes.Length >= 6
            && bytes[0] == 0x47
            && bytes[1] == 0x49
            && bytes[2] == 0x46)
        {
            extension = ".gif";
            return true;
        }

        if (bytes.Length >= 12
            && bytes[0] == 0x52
            && bytes[1] == 0x49
            && bytes[2] == 0x46
            && bytes[3] == 0x46
            && bytes[8] == 0x57
            && bytes[9] == 0x45
            && bytes[10] == 0x42
            && bytes[11] == 0x50)
        {
            extension = ".webp";
            return true;
        }

        return false;
    }
}
