using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using EnhancedQuickPanel.Models;

namespace EnhancedQuickPanel.Services;

/// <summary>Draws optional Font Awesome marks in slot corners. Dalamud type icons and optional Dev wrenches lock the bottom-right.</summary>
internal static class SlotCornerBadgeDrawer
{
    public const float MinScale = 0.5f;

    public const float MaxScale = 2f;
    public static bool TryGetLockedBottomRight(PanelSlot slot, out FontAwesomeIcon icon, out string tooltipKey)
    {
        if (slot.Kind == PanelSlotKind.Dalamud)
        {
            icon = DalamudShortcuts.TryGet(slot.DalamudShortcut, out var preset)
                ? preset.Icon
                : FontAwesomeIcon.PuzzlePiece;
            tooltipKey = "slot.corner.dalamudLocked";
            return true;
        }

        if (IsDevWrenchLocked(slot))
        {
            icon = FontAwesomeIcon.Wrench;
            tooltipKey = "slot.corner.devLocked";
            return true;
        }

        icon = default;
        tooltipKey = string.Empty;
        return false;
    }

    private static bool IsDevWrenchLocked(PanelSlot slot) =>
        PluginShortcuts.IsDev(slot) && Config.LockDevPluginWrench;

    public static void Draw(
        ImDrawListPtr drawList,
        Vector2 min,
        Vector2 max,
        PanelSlot slot,
        bool isGrayedOut)
    {
        var color = GetDrawColor(slot, isGrayedOut);

        DrawCorner(drawList, min, max, SlotCorner.TopLeft, slot.BadgeTopLeft, color);
        DrawCorner(drawList, min, max, SlotCorner.TopRight, slot.BadgeTopRight, color);
        DrawCorner(drawList, min, max, SlotCorner.BottomLeft, slot.BadgeBottomLeft, color);
        if (TryGetLockedBottomRight(slot, out var lockedIcon, out _))
        {
            var lockedColor = IsDevWrenchLocked(slot)
                ? GetWrenchDrawColor(isGrayedOut)
                : color;
            DrawIcon(drawList, min, max, SlotCorner.BottomRight, lockedIcon, lockedColor);
        }
        else
            DrawCorner(drawList, min, max, SlotCorner.BottomRight, slot.BadgeBottomRight, color);
    }

    public static Vector4 GetColor(PanelSlot slot) =>
        slot.BadgeUseCustomColor ? slot.BadgeColor : Config.CornerBadgeColor;

    private static uint GetDrawColor(PanelSlot slot, bool isGrayedOut)
    {
        var color = GetColor(slot);
        if (isGrayedOut)
            color = Dim(color);

        return ImGui.ColorConvertFloat4ToU32(color);
    }

    private static uint GetWrenchDrawColor(bool isGrayedOut)
    {
        var color = Config.DevPluginWrenchColor;
        if (isGrayedOut)
            color = Dim(color);

        return ImGui.ColorConvertFloat4ToU32(color);
    }

    private static Vector4 Dim(Vector4 color) =>
        new(color.X * 0.5f, color.Y * 0.5f, color.Z * 0.5f, color.W);

    private static void DrawCorner(
        ImDrawListPtr drawList,
        Vector2 min,
        Vector2 max,
        SlotCorner corner,
        ushort value,
        uint color)
    {
        if (!TryResolveIcon(value, out var icon))
            return;

        DrawIcon(drawList, min, max, corner, icon, color);
    }

    private static void DrawIcon(
        ImDrawListPtr drawList,
        Vector2 min,
        Vector2 max,
        SlotCorner corner,
        FontAwesomeIcon icon,
        uint color)
    {
        var slotSize = max - min;
        var fontSize = FontSizeFor(slotSize);
        var textSize = Measure(icon, fontSize);
        var pos = Position(min, max, corner, textSize);
        drawList.AddText(UiBuilder.IconFont, fontSize, pos, color, icon.ToIconString());
    }

    public static float FontSizeFor(Vector2 slotSize) =>
        (Math.Max(8f, slotSize.Y * 0.26f) + 1f) * Scale();

    private static float InsetFor(Vector2 slotSize) =>
        Math.Max(2f, slotSize.X * 0.08f) * Scale();

    private static float Scale() =>
        Config.CornerBadgeScale <= 0f ? 1f : Math.Clamp(Config.CornerBadgeScale, MinScale, MaxScale);

    public static Vector2 Measure(FontAwesomeIcon icon, float fontSize)
    {
        using (ImRaii.PushFont(UiBuilder.IconFont))
            return ImGui.CalcTextSize(icon.ToIconString()) * (fontSize / ImGui.GetFontSize());
    }

    public static Vector2 Position(Vector2 min, Vector2 max, SlotCorner corner, Vector2 textSize)
    {
        var inset = InsetFor(max - min);
        return corner switch
        {
            SlotCorner.TopLeft => new Vector2(min.X + inset, min.Y + inset),
            SlotCorner.TopRight => new Vector2(max.X - inset - textSize.X, min.Y + inset),
            SlotCorner.BottomLeft => new Vector2(min.X + inset, max.Y - inset - textSize.Y),
            _ => new Vector2(max.X - inset - textSize.X, max.Y - inset - textSize.Y),
        };
    }

    internal static bool TryResolveIcon(ushort value, out FontAwesomeIcon icon)
    {
        icon = (FontAwesomeIcon)value;
        if (value == 0)
            return false;
        return Enum.IsDefined(icon);
    }
}
