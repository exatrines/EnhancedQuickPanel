using System.Text;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using EnhancedQuickPanel.Services.CustomIcons;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EnhancedQuickPanel.Services;

/// <summary>Resolves and loads the texture for a slot icon from game or custom icon sources.</summary>
internal static unsafe class SlotTextureResolver
{
    private static readonly IconSubFolder[] IconSubFoldersToTry =
    [
        IconSubFolder.None,
        IconSubFolder.HighQuality,
    ];

    private static readonly bool[] HqFirst = [true, false];
    private static readonly bool[] NqFirst = [false, true];
    private static readonly bool[] HiResOrder = [true, false];
    private static readonly int[] NativeScales = [1, 2];
    private static readonly Dictionary<GameIconKey, ISharedImmediateTexture> SharedCache = [];

    public static void ClearCache() => SharedCache.Clear();

    public static bool TryGetSlotTexture(ResolvedSlotIcon icon, out IDalamudTextureWrap texture) =>
        TryGetSlotTexture(-1, icon, out texture);

    public static bool TryGetSlotTexture(int slotIndex, ResolvedSlotIcon icon, out IDalamudTextureWrap texture)
    {
        texture = null!;

        if (CustomIconIds.IsCustom(icon.IconId))
            return CustomIconRegistry.TryGetTexture(icon.IconId, out texture);

        if (icon.IsValid && TryGetGameIconTexture(icon, out texture))
            return true;

        if (icon.IsHighQuality && TryGetGameIconTexture(icon with { IsHighQuality = false }, out texture))
            return true;

        if (slotIndex >= 0 && TryGetNativeIconImageTexture(slotIndex, out texture))
            return true;

        return false;
    }

    private static bool TryGetGameIconTexture(ResolvedSlotIcon icon, out IDalamudTextureWrap texture)
    {
        texture = null!;
        if (!icon.IsValid || CustomIconIds.IsCustom(icon.IconId))
            return false;

        var key = new GameIconKey(icon.IconId, icon.IsHighQuality);
        if (SharedCache.TryGetValue(key, out var cached))
        {
            if (TryGetSharedWrap(cached, out texture))
                return true;

            SharedCache.Remove(key);
        }

        foreach (var isHighQuality in icon.IsHighQuality ? HqFirst : NqFirst)
        {
            foreach (var hiRes in HiResOrder)
            {
                if (TryGetImmediateGameIcon(icon.IconId, isHighQuality, hiRes, out var shared, out texture))
                {
                    SharedCache[key] = shared;
                    return true;
                }
            }
        }

        if (!TryGetNativeIconTexture(icon.IconId, out var nativeShared, out texture))
            return false;

        SharedCache[key] = nativeShared;
        return true;
    }

    private static bool TryGetImmediateGameIcon(
        uint iconId,
        bool isHighQuality,
        bool hiRes,
        out ISharedImmediateTexture shared,
        out IDalamudTextureWrap texture)
    {
        shared = null!;
        texture = null!;
        if (CustomIconIds.IsCustom(iconId))
            return false;

        var lookup = new GameIconLookup(iconId, itemHq: isHighQuality, hiRes: hiRes);

        if (PluginServices.Texture.TryGetFromGameIcon(lookup, out var found)
            && TryGetSharedWrap(found, out texture))
        {
            shared = found;
            return true;
        }

        if (PluginServices.Texture.TryGetIconPath(lookup, out var path)
            && TryGetGameTexture(path, out shared, out texture))
        {
            return true;
        }

        shared = null!;
        return false;
    }

    private static bool TryGetNativeIconTexture(
        uint iconId,
        out ISharedImmediateTexture shared,
        out IDalamudTextureWrap texture)
    {
        shared = null!;
        texture = null!;
        if (CustomIconIds.IsCustom(iconId) || !GameModuleGuard.IsClientReady)
            return false;

        Span<byte> buffer = stackalloc byte[256];
        foreach (var folder in IconSubFoldersToTry)
        {
            foreach (var scale in NativeScales)
            {
                buffer.Clear();
                fixed (byte* bufferPtr = buffer)
                {
                    var length = AtkTexture.GetIconPath(bufferPtr, iconId, scale, folder);
                    if (length <= 0)
                        continue;

                    var path = Encoding.UTF8.GetString(bufferPtr, length);
                    if (TryGetGameTexture(path, out shared, out texture))
                        return true;
                }
            }
        }

        return false;
    }

    private static bool TryGetGameTexture(
        string? path,
        out ISharedImmediateTexture shared,
        out IDalamudTextureWrap texture)
    {
        shared = null!;
        texture = null!;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        shared = PluginServices.Texture.GetFromGame(path);
        return TryGetSharedWrap(shared, out texture);
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
        if (!IsUsableWrap(wrap))
            return false;
        texture = wrap!;
        return true;
    }

    private static bool IsUsableWrap(IDalamudTextureWrap? wrap)
    {
        if (wrap == null)
            return false;
        try
        {
            return wrap.Handle != 0 && wrap.Width > 1 && wrap.Height > 1;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private static bool TryGetNativeIconImageTexture(int slotIndex, out IDalamudTextureWrap texture)
    {
        texture = null!;
        if (!NativeQuickPanelUiReader.TryGetSlotIconImagePath(slotIndex, out var texturePath))
            return false;

        return TryGetGameTexture(texturePath, out _, out texture);
    }

    private readonly record struct GameIconKey(uint IconId, bool HighQuality);
}
