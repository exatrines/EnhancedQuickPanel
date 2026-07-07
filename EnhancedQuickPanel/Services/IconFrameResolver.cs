using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;

namespace EnhancedQuickPanel.Services;

/// <summary>Draws icon frames and hover frames using nine-slice frame parts.</summary>
internal static class IconFrameResolver
{
    private const string TexturePath = "ui/uld/IconA_Frame_hr1.tex";

    // Native AtkComponentIcon.IconImage is authored at 40x40 design pixels.
    private const float NativeIconDesignSize = 40f;

    // Part #00 from IconA_Frame_hr1 (Addon Inspector).
    private static readonly FramePart IconFramePart = new(0, 0, 48, 48, 0, 0, 96, 96);

    // Part #16 hover glow from IconA_Frame_hr1 (Addon Inspector).
    private static readonly FramePart HoverFramePart = new(240, 0, 72, 72, 480, 0, 144, 144);

    private static ISharedImmediateTexture? _sharedTexture;

    public static void DrawIconFrame(
        ImDrawListPtr drawList,
        Vector2 min,
        Vector2 max,
        bool isGrayedOut) =>
        DrawScaledPart(drawList, min, max, IconFramePart, isGrayedOut);

    public static void DrawHoverFrame(
        ImDrawListPtr drawList,
        Vector2 min,
        Vector2 max,
        bool isGrayedOut) =>
        DrawScaledPart(drawList, min, max, HoverFramePart, isGrayedOut);

    private static void DrawScaledPart(
        ImDrawListPtr drawList,
        Vector2 min,
        Vector2 max,
        FramePart part,
        bool isGrayedOut)
    {
        var sizeScale = part.StdW / NativeIconDesignSize;
        var center = (min + max) * 0.5f;
        var halfSize = (max - min) * 0.5f * sizeScale;
        DrawPart(drawList, center - halfSize, center + halfSize, part, isGrayedOut);
    }

    private static void DrawPart(
        ImDrawListPtr drawList,
        Vector2 min,
        Vector2 max,
        FramePart part,
        bool isGrayedOut)
    {
        if (!TryGetPartTexture(part, out var texture, out var uv0, out var uv1))
            return;

        var tint = isGrayedOut
            ? ImGui.ColorConvertFloat4ToU32(new Vector4(0.5f, 0.5f, 0.5f, 1f))
            : uint.MaxValue;

        SafeTextureDraw.TryAddImage(drawList, texture, min, max, uv0, uv1, tint);
    }

    private static bool TryGetPartTexture(
        FramePart part,
        out IDalamudTextureWrap texture,
        out Vector2 uv0,
        out Vector2 uv1)
    {
        texture = null!;
        uv0 = Vector2.Zero;
        uv1 = Vector2.One;

        _sharedTexture ??= Svc.Texture.GetFromGame(TexturePath);

        var wrap = _sharedTexture.GetWrapOrDefault();
        if (wrap == null || wrap.Width <= 0 || wrap.Height <= 0)
            return false;

        if (!TryComputePartUv(wrap, part, out uv0, out uv1))
            return false;

        texture = wrap;
        return true;
    }

    private static bool TryComputePartUv(
        IDalamudTextureWrap texture,
        FramePart part,
        out Vector2 uv0,
        out Vector2 uv1)
    {
        uv0 = Vector2.Zero;
        uv1 = Vector2.One;

        var useHiRes = texture.Width >= part.HiResX + part.HiResW;
        var x = useHiRes ? part.HiResX : part.StdX;
        var y = useHiRes ? part.HiResY : part.StdY;
        var w = useHiRes ? part.HiResW : part.StdW;
        var h = useHiRes ? part.HiResH : part.StdH;

        if (x + w > texture.Width || y + h > texture.Height)
            return false;

        uv0 = new Vector2(x / (float)texture.Width, y / (float)texture.Height);
        uv1 = new Vector2((x + w) / (float)texture.Width, (y + h) / (float)texture.Height);
        return true;
    }

    /// <summary>One part of a nine-slice icon frame texture.</summary>
    private readonly record struct FramePart(
        int StdX,
        int StdY,
        int StdW,
        int StdH,
        int HiResX,
        int HiResY,
        int HiResW,
        int HiResH);
}

