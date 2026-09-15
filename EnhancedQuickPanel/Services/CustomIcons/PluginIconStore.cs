using System.Collections.Concurrent;
using System.Threading;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using EnhancedQuickPanel.Services;

namespace EnhancedQuickPanel.Services.CustomIcons;

/// <summary>Resolves plugin shortcut icons: IsDev disk file, then icon/Plugins, then IconUrl/Dip17 download.</summary>
internal static class PluginIconStore
{
    private const string FolderName = "Plugins";
    private const string Dip17IconUrl =
        "https://raw.githubusercontent.com/goatcorp/PluginDistD17/main/{0}/{1}/images/icon.png";

    private static readonly ConcurrentDictionary<string, ISharedImmediateTexture> TextureCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string> PathByStem = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, byte> InFlight = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, byte> Failed = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, byte> MissingFiles = new(StringComparer.OrdinalIgnoreCase);

    private static string _directory = string.Empty;

    public static void Initialize()
    {
        _directory = Path.Combine(CustomIconRegistry.IconDirectory, FolderName);
        Directory.CreateDirectory(_directory);
    }

    public static void Dispose()
    {
        TextureCache.Clear();
        PathByStem.Clear();
        InFlight.Clear();
        Failed.Clear();
        MissingFiles.Clear();
    }

    public static void Invalidate() => MissingFiles.Clear();

    public static bool IsDownloading(string internalName) =>
        !string.IsNullOrWhiteSpace(internalName) && InFlight.ContainsKey(internalName);

    public static bool TryGet(
        IExposedPlugin? plugin,
        string internalName,
        string? devIconPath,
        out IDalamudTextureWrap texture)
    {
        texture = null!;
        if (string.IsNullOrWhiteSpace(internalName))
            return false;

        if (!string.IsNullOrEmpty(devIconPath) && TryGetFromFile(devIconPath, out texture))
            return true;
        if (TryGetStored(internalName, out texture))
            return true;
        if (plugin != null)
            RequestDownload(internalName, ResolveIconUrl(plugin));
        return false;
    }

    private static bool TryGetStored(string internalName, out IDalamudTextureWrap texture)
    {
        texture = null!;
        if (string.IsNullOrEmpty(_directory) || !TryFindFile(FileStem(internalName), out var path))
            return false;
        return TryGetFromFile(path, out texture);
    }

    private static bool TryGetFromFile(string path, out IDalamudTextureWrap texture)
    {
        texture = null!;
        if (TextureCache.TryGetValue(path, out var cached))
        {
            if (TryGetSharedWrap(cached, out texture))
                return true;
            TextureCache.TryRemove(path, out _);
        }

        if (MissingFiles.ContainsKey(path))
            return false;
        if (!File.Exists(path))
        {
            MissingFiles.TryAdd(path, 0);
            return false;
        }

        try
        {
            var shared = TextureCache.GetOrAdd(path, static filePath => PluginServices.Texture.GetFromFile(filePath));
            if (TryGetSharedWrap(shared, out texture))
                return true;
            TextureCache.TryRemove(path, out _);
            return false;
        }
        catch (Exception ex)
        {
            MissingFiles.TryAdd(path, 0);
            PluginLifetime.TryLogDebug($"[EQP] Plugin icon load failed ({path}): {ex.Message}");
            return false;
        }
    }

    private static bool TryGetSharedWrap(ISharedImmediateTexture shared, out IDalamudTextureWrap texture)
    {
        texture = null!;
        if (TryUseWrap(shared.GetWrapOrDefault(), out texture))
            return true;
        return shared.TryGetWrap(out var wrap, out _) && TryUseWrap(wrap, out texture);
    }

    private static bool TryUseWrap(IDalamudTextureWrap? wrap, out IDalamudTextureWrap texture)
    {
        texture = null!;
        if (wrap == null)
            return false;
        try
        {
            if (wrap.Handle == 0 || wrap.Width <= 1 || wrap.Height <= 1)
                return false;
            texture = wrap;
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private static void RequestDownload(string internalName, string? iconUrl)
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
        if (InFlight.Count == 1)
            Notifications.Info(T("shortcut.iconDownloading"));

        var name = internalName;
        var url = iconUrl.Trim();
        var token = PluginLifetime.Token;
        _ = Task.Run(() => DownloadAsync(name, url, token));
    }

    private static bool NeedsDownload(string internalName)
    {
        if (PluginLifetime.IsStopping)
            return false;
        if (string.IsNullOrWhiteSpace(internalName) || string.IsNullOrEmpty(_directory))
            return false;
        if (Failed.ContainsKey(internalName) || InFlight.ContainsKey(internalName))
            return false;
        return !TryFindFile(FileStem(internalName), out _);
    }

    private static string? ResolveIconUrl(IExposedPlugin plugin)
    {
        if (plugin.IsThirdParty || plugin.IsDev)
        {
            var iconUrl = ReadManifestString(plugin, "IconUrl");
            return string.IsNullOrWhiteSpace(iconUrl) ? null : iconUrl.Trim();
        }

        var channel = ReadManifestString(plugin, "Dip17Channel");
        if (string.IsNullOrWhiteSpace(channel))
            return null;
        return string.Format(Dip17IconUrl, channel, plugin.InternalName);
    }

    private static string? ReadManifestString(IExposedPlugin plugin, string propertyName) =>
        plugin.Manifest.GetType().GetProperty(propertyName)?.GetValue(plugin.Manifest) as string;

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
        if (PathByStem.TryGetValue(stem, out var cached) && File.Exists(cached))
        {
            path = cached;
            return true;
        }

        if (!Directory.Exists(_directory))
            return false;

        foreach (var filePath in Directory.EnumerateFiles(_directory))
        {
            if (!CustomIconFileNames.IsSupportedImageExtension(Path.GetExtension(filePath)))
                continue;
            if (!string.Equals(CustomIconFileNames.GetStem(filePath), stem, StringComparison.OrdinalIgnoreCase))
                continue;
            PathByStem[stem] = filePath;
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
        PathByStem.TryRemove(stem, out _);
        foreach (var key in TextureCache.Keys)
        {
            if (string.Equals(CustomIconFileNames.GetStem(key), stem, StringComparison.OrdinalIgnoreCase))
                TextureCache.TryRemove(key, out _);
        }
    }
}
