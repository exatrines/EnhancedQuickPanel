using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EnhancedQuickPanel.Services;

/// <summary>A texture plus the UV rectangle to draw from it.</summary>
internal readonly record struct SlotTextureSlice(IDalamudTextureWrap Texture, Vector2 Uv0, Vector2 Uv1);

/// <summary>Resolves the texture and UV region used to draw a slot icon.</summary>
internal static unsafe class SlotImageResolver
{
    private static readonly Dictionary<string, ISharedImmediateTexture> SharedTextures = new(StringComparer.OrdinalIgnoreCase);

    public static bool UsesTransparentSlotBackground(RaptureHotbarModule.HotbarSlotType type) =>
        type is RaptureHotbarModule.HotbarSlotType.Marker
            or RaptureHotbarModule.HotbarSlotType.FieldMarker;

    public static bool TryGetSlotFrameSlice(int slotIndex, out SlotTextureSlice slice) =>
        NativeQuickPanelUiReader.TryGetSlotFrameSlice(slotIndex, out slice);

    public static bool TryGetSlotIconSlice(int slotIndex, out SlotTextureSlice slice) =>
        NativeQuickPanelUiReader.TryGetSlotIconSlice(slotIndex, out slice);

    internal static bool TryGetImageNodeSlice(AtkImageNode* imageNode, out SlotTextureSlice slice)
    {
        slice = default;
        if (imageNode == null)
            return false;

        if (!NativeQuickPanelUiReader.TryGetImageNodeTexturePath(imageNode, out var texturePath))
            return false;

        if (!TryGetTextureWrap(texturePath, out var texture))
            return false;

        if (imageNode->PartsList == null || imageNode->PartId >= imageNode->PartsList->PartCount)
            return false;

        var part = imageNode->PartsList->Parts[imageNode->PartId];
        if (!TryComputePartUv(texture, part, out var uv0, out var uv1))
            return false;

        slice = new SlotTextureSlice(texture, uv0, uv1);
        return true;
    }

    private static bool TryGetTextureWrap(string texturePath, out IDalamudTextureWrap texture)
    {
        texture = null!;
        if (string.IsNullOrWhiteSpace(texturePath))
            return false;

        if (!SharedTextures.TryGetValue(texturePath, out var shared))
        {
            shared = Svc.Texture.GetFromGame(texturePath);
            SharedTextures[texturePath] = shared;
        }

        var wrap = shared.GetWrapOrDefault();
        if (wrap == null)
            return false;

        texture = wrap;
        return true;
    }

    private static bool TryComputePartUv(
        IDalamudTextureWrap texture,
        AtkUldPart part,
        out Vector2 uv0,
        out Vector2 uv1)
    {
        uv0 = Vector2.Zero;
        uv1 = Vector2.One;

        if (texture.Width <= 0 || texture.Height <= 0)
            return false;

        var scale = texture.Width >= 800 ? 2f : 1f;
        var u = part.U * scale;
        var v = part.V * scale;
        var w = part.Width * scale;
        var h = part.Height * scale;

        uv0 = new Vector2(u / texture.Width, v / texture.Height);
        uv1 = new Vector2((u + w) / texture.Width, (v + h) / texture.Height);
        return true;
    }
}

