namespace EnhancedQuickPanel.Services;

/// <summary>Draws slot background frames and drop-target highlights.</summary>
internal static class SlotBackgroundResolver
{
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
        var color = C.SlotDropTargetColor;
        if (iconGrayedOut)
            color = new Vector4(color.X, color.Y, color.Z, color.W * 0.85f);

        drawList.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(color), rounding);
    }

    private static uint GetFillColor(bool iconGrayedOut, bool isDropTarget)
    {
        var color = isDropTarget ? C.SlotDropTargetColor : C.SlotBgColor;
        if (iconGrayedOut)
            color = new Vector4(color.X * 0.65f, color.Y * 0.65f, color.Z * 0.65f, color.W);

        return ImGui.ColorConvertFloat4ToU32(color);
    }

    private static uint GetBorderColor(bool iconGrayedOut, bool isDropTarget)
    {
        var color = isDropTarget ? C.SlotDropTargetColor : C.SlotBgColor;
        color = new Vector4(color.X * 0.6f, color.Y * 0.6f, color.Z * 0.6f, color.W);
        if (iconGrayedOut)
            color = new Vector4(color.X * 0.65f, color.Y * 0.65f, color.Z * 0.65f, color.W);

        return ImGui.ColorConvertFloat4ToU32(color);
    }
}

