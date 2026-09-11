using System.Collections.Concurrent;
using System.Threading;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using EnhancedQuickPanel.Services;

namespace EnhancedQuickPanel.Services.CustomIcons;

/// <summary>Caches plugin icons under icon/Plugins. Not shown in the custom icon picker.</summary>
internal static class PluginIconStore
{
    private const string FolderName = "Plugins";

    private static readonly ConcurrentDictionary<string, ISharedImmediateTexture> TextureCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, byte> InFlight = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, byte> Failed = new(StringComparer.Ordinal);

    private static string _directory = string.Empty;

    public static void Initialize()
    {
        _directory = Path.Combine(CustomIconRegistry.IconDirectory, FolderName);
        Directory.CreateDirectory(_directory);
    }

    public static void Dispose()
    {
        TextureCache.Clear();
        InFlight.Clear();
        Failed.Clear();
    }

    public static bool TryGetTexture(string internalName, out IDalamudTextureWrap texture)
    {
        texture = null!;
        if (string.IsNullOrWhiteSpace(internalName) || string.IsNullOrEmpty(_directory))
            return false;
        if (!TryFindFile(FileStem(internalName), out var path))
            return false;

        try
        {
            var shared = TextureCache.GetOrAdd(path, static filePath => PluginServices.Texture.GetFromFile(filePath));
            var wrap = shared.GetWrapOrDefault();
            if (wrap == null || wrap.Handle == 0)
                return false;

            texture = wrap;
            return true;
        }
        catch (Exception ex)
        {
            PluginLifetime.TryLogDebug($"[EQP] Plugin icon load failed ({path}): {ex.Message}");
            return false;
        }
    }

    public static bool NeedsDownload(string internalName)
    {
        if (PluginLifetime.IsStopping)
            return false;
        if (string.IsNullOrWhiteSpace(internalName) || string.IsNullOrEmpty(_directory))
            return false;
        if (Failed.ContainsKey(internalName) || InFlight.ContainsKey(internalName))
            return false;
        return !TryFindFile(FileStem(internalName), out _);
    }

    public static void RequestDownload(string internalName, string? iconUrl)
    {
        if (PluginLifetime.IsStopping || !NeedsDownload(internalName))
            return;
        if (string.IsNullOrWhiteSpace(iconUrl))
        {
            Failed.TryAdd(internalName, 0);
            return;
        }
        if (!InFlight.TryAdd(internalName, 0))
            return;

        var name = internalName;
        var url = iconUrl.Trim();
        var token = PluginLifetime.Token;
        _ = Task.Run(() => DownloadAsync(name, url, token));
    }

    private static async Task DownloadAsync(string internalName, string iconUrl, CancellationToken cancellationToken)
    {
        try
        {
            if (!Uri.TryCreate(iconUrl, UriKind.Absolute, out var uri)
                || uri.Scheme is not "http" and not "https")
            {
                Failed.TryAdd(internalName, 0);
                return;
            }

            var result = await BoundedHttpDownload.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            if (result.Status != BoundedDownloadStatus.Success
                || !CustomIconRegistry.TryDetectImageExtension(result.Bytes, out var extension))
            {
                if (result.Status != BoundedDownloadStatus.Canceled)
                    Failed.TryAdd(internalName, 0);
                return;
            }

            Directory.CreateDirectory(_directory);
            var stem = FileStem(internalName);
            var destPath = Path.Combine(_directory, stem + extension);
            var committed = await PluginLifetime.WriteAndCommitAsync(
                destPath,
                result.Bytes,
                cancellationToken,
                () => RemoveOtherFiles(stem, destPath)).ConfigureAwait(false);
            if (committed)
                InvalidateStem(stem);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Failed.TryAdd(internalName, 0);
            PluginLifetime.TryLogDebug($"[EQP] Plugin icon download failed ({internalName}): {ex.Message}");
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            InFlight.TryRemove(internalName, out _);
        }
    }

    private static string FileStem(string internalName) =>
        CustomIconFileNames.NormalizeStemForNewFile(internalName);

    private static bool TryFindFile(string stem, out string path)
    {
        path = string.Empty;
        if (!Directory.Exists(_directory))
            return false;

        foreach (var filePath in Directory.EnumerateFiles(_directory))
        {
            if (!CustomIconFileNames.IsSupportedImageExtension(Path.GetExtension(filePath)))
                continue;
            if (!string.Equals(CustomIconFileNames.GetStem(filePath), stem, StringComparison.OrdinalIgnoreCase))
                continue;
            path = filePath;
            return true;
        }

        return false;
    }

    private static void RemoveOtherFiles(string stem, string keepPath)
    {
        foreach (var filePath in Directory.EnumerateFiles(_directory))
        {
            if (string.Equals(filePath, keepPath, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(CustomIconFileNames.GetStem(filePath), stem, StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                PluginLifetime.TryLogDebug($"[EQP] Could not replace plugin icon {filePath}: {ex.Message}");
            }
        }
    }

    private static void InvalidateStem(string stem)
    {
        foreach (var key in TextureCache.Keys)
        {
            if (string.Equals(CustomIconFileNames.GetStem(key), stem, StringComparison.OrdinalIgnoreCase))
                TextureCache.TryRemove(key, out _);
        }
    }
}
