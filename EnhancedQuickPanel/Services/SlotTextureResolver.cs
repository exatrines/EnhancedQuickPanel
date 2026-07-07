using System.Text;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using ECommons.ImGuiMethods;
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

        foreach (var isHighQuality in icon.IsHighQuality ? new[] { true, false } : new[] { false, true })
        {
            foreach (var hiRes in new[] { true, false })
            {
                if (TryGetImmediateGameIconWrap(icon.IconId, isHighQuality, hiRes, out texture))
                    return true;
            }
        }

        if (ThreadLoadImageHandler.TryGetIconTextureWrap(icon.IconId, icon.IsHighQuality, out texture)
            && IsUsableWrap(texture))
        {
            return true;
        }

        if (icon.IsHighQuality
            && ThreadLoadImageHandler.TryGetIconTextureWrap(icon.IconId, false, out texture)
            && IsUsableWrap(texture))
        {
            return true;
        }

        if (TryGetNativeIconTexture(icon.IconId, out texture))
            return true;

        ThreadLoadImageHandler.TryGetIconTextureWrap(icon.IconId, icon.IsHighQuality, out _);
        return false;
    }

    private static bool TryGetImmediateGameIconWrap(
        uint iconId,
        bool isHighQuality,
        bool hiRes,
        out IDalamudTextureWrap texture)
    {
        texture = null!;
        if (CustomIconIds.IsCustom(iconId))
            return false;

        var lookup = new GameIconLookup(iconId, itemHq: isHighQuality, hiRes: hiRes);

        if (Svc.Texture.TryGetFromGameIcon(lookup, out var shared))
        {
            var sharedWrap = shared.GetWrapOrDefault();
            if (sharedWrap != null && IsUsableWrap(sharedWrap))
            {
                texture = sharedWrap;
                return true;
            }

            if (shared.TryGetWrap(out var wrap, out _) && wrap != null && IsUsableWrap(wrap))
            {
                texture = wrap;
                return true;
            }
        }

        if (Svc.Texture.TryGetIconPath(lookup, out var path)
            && TryGetGameTexture(path, out texture))
        {
            return true;
        }

        return false;
    }

    private static bool TryGetNativeIconTexture(uint iconId, out IDalamudTextureWrap texture)
    {
        texture = null!;
        if (CustomIconIds.IsCustom(iconId) || !GameModuleGuard.IsClientReady)
            return false;

        Span<byte> buffer = stackalloc byte[256];
        foreach (var folder in IconSubFoldersToTry)
        {
            foreach (var scale in new[] { 1, 2 })
            {
                buffer.Clear();
                fixed (byte* bufferPtr = buffer)
                {
                    var length = AtkTexture.GetIconPath(bufferPtr, iconId, scale, folder);
                    if (length <= 0)
                        continue;

                    var path = Encoding.UTF8.GetString(bufferPtr, length);
                    if (TryGetGameTexture(path, out texture))
                        return true;
                }
            }
        }

        return false;
    }

    private static bool TryGetGameTexture(string? path, out IDalamudTextureWrap texture)
    {
        texture = null!;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (ThreadLoadImageHandler.TryGetTextureWrap(path, out texture) && IsUsableWrap(texture))
            return true;

        var wrap = Svc.Texture.GetFromGame(path).GetWrapOrDefault();
        if (wrap == null || !IsUsableWrap(wrap))
            return false;

        texture = wrap;
        return true;
    }

    private static bool IsUsableWrap(IDalamudTextureWrap? wrap) =>
        wrap != null && wrap.Handle != 0 && wrap.Width > 1 && wrap.Height > 1;

    private static bool TryGetNativeIconImageTexture(int slotIndex, out IDalamudTextureWrap texture)
    {
        texture = null!;
        if (!NativeQuickPanelUiReader.TryGetSlotIconImagePath(slotIndex, out var texturePath))
            return false;

        return TryGetGameTexture(texturePath, out texture);
    }
}

