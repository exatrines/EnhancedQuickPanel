using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;

namespace EnhancedQuickPanel.Services;

/// <summary>Draws slot backgrounds, drop-target highlights, and nine-slice icon frames.</summary>
internal static class SlotChromeDrawer
{
    private const string TexturePath = "ui/uld/IconA_Frame_hr1.tex";
    private const float NativeIconDesignSize = 40f;

    private static readonly FramePart IconFramePart = new(0, 0, 48, 48, 0, 0, 96, 96);
    private static readonly FramePart HoverFramePart = new(240, 0, 72, 72, 480, 0, 144, 144);

    private static ISharedImmediateTexture? _sharedTexture;

    public static void DrawBaseFrame(
        ImDrawListPtr drawList,
        Vector2 min,
        Vector2 max,
        bool iconGrayedOut,
        bool isDropTarget = false)
    {
        var size = max - min;
        var rounding = Math.Clamp(size.X * 0.11f, 3f, 6f);
        var fill = GetFillColor(iconGrayedOut, isDropTarget);
        var border = GetBorderColor(iconGrayedOut, isDropTarget);

        drawList.AddRectFilled(min, max, fill, rounding);
        drawList.AddRect(min, max, border, rounding, ImDrawFlags.RoundCornersAll, 1f);
    }

    public static void DrawDropTargetOverlay(
        ImDrawListPtr drawList,
        Vector2 min,
        Vector2 max,
        bool iconGrayedOut = false)
    {
        var size = max - min;
        var rounding = Math.Clamp(size.X * 0.11f, 3f, 6f);
        var color = Config.SlotDropTargetColor;
        if (iconGrayedOut)
            color = new Vector4(color.X, color.Y, color.Z, color.W * 0.85f);

        drawList.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(color), rounding);
    }

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

    private static uint GetFillColor(bool iconGrayedOut, bool isDropTarget)
    {
        var color = isDropTarget ? Config.SlotDropTargetColor : Config.SlotBgColor;
        if (iconGrayedOut)
            color = new Vector4(color.X * 0.65f, color.Y * 0.65f, color.Z * 0.65f, color.W);

        return ImGui.ColorConvertFloat4ToU32(color);
    }

    private static uint GetBorderColor(bool iconGrayedOut, bool isDropTarget)
    {
        var color = isDropTarget ? Config.SlotDropTargetColor : Config.SlotBgColor;
        color = new Vector4(color.X * 0.6f, color.Y * 0.6f, color.Z * 0.6f, color.W);
        if (iconGrayedOut)
            color = new Vector4(color.X * 0.65f, color.Y * 0.65f, color.Z * 0.65f, color.W);

        return ImGui.ColorConvertFloat4ToU32(color);
    }

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

        _sharedTexture ??= PluginServices.Texture.GetFromGame(TexturePath);

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
