using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using EnhancedQuickPanel.Models;

namespace EnhancedQuickPanel.Services;

/// <summary>Draws optional Font Awesome marks in slot corners. Dalamud type icons and optional Dev wrenches lock the bottom-right.</summary>
internal static class SlotCornerBadgeDrawer
{
    public const float MinScale = SlotCornerLook.MinScale;

    public const float MaxScale = SlotCornerLook.MaxScale;

    public static bool TryGetLockedBottomRight(PanelSlot slot, out FontAwesomeIcon icon)
    {
        if (slot.Kind == PanelSlotKind.Dalamud)
        {
            icon = DalamudShortcuts.TryGet(slot.DalamudShortcut, out var preset)
                ? preset.Icon
                : FontAwesomeIcon.PuzzlePiece;
            return true;
        }

        if (IsDevWrenchLocked(slot))
        {
            icon = FontAwesomeIcon.Wrench;
            return true;
        }

        icon = default;
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
        DrawCorner(drawList, min, max, SlotCorner.TopLeft, slot.BadgeTopLeft, slot, isGrayedOut);
        DrawCorner(drawList, min, max, SlotCorner.TopRight, slot.BadgeTopRight, slot, isGrayedOut);
        DrawCorner(drawList, min, max, SlotCorner.BottomLeft, slot.BadgeBottomLeft, slot, isGrayedOut);
        if (TryGetLockedBottomRight(slot, out var lockedIcon))
            DrawIcon(
                drawList,
                min,
                max,
                SlotCorner.BottomRight,
                lockedIcon,
                GetDrawColor(slot, SlotCorner.BottomRight, isGrayedOut),
                slot);
        else
            DrawCorner(drawList, min, max, SlotCorner.BottomRight, slot.BadgeBottomRight, slot, isGrayedOut);
    }

    public static Vector4 GetColor(PanelSlot slot, SlotCorner corner)
    {
        var look = slot.GetCornerLook(corner);
        if (look.UseCustomColor)
            return look.Color;
        if (IsDevWrenchLocked(slot) && corner == SlotCorner.BottomRight)
            return Config.DevPluginWrenchColor;
        if (slot.BadgeUseCustomColor)
            return slot.BadgeColor;
        return Config.CornerBadgeColor;
    }

    private static uint GetDrawColor(PanelSlot slot, SlotCorner corner, bool isGrayedOut)
    {
        var color = GetColor(slot, corner);
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
        PanelSlot slot,
        bool isGrayedOut)
    {
        if (!TryResolveIcon(value, out var icon))
            return;

        DrawIcon(drawList, min, max, corner, icon, GetDrawColor(slot, corner, isGrayedOut), slot);
    }

    private static void DrawIcon(
        ImDrawListPtr drawList,
        Vector2 min,
        Vector2 max,
        SlotCorner corner,
        FontAwesomeIcon icon,
        uint color,
        PanelSlot slot)
    {
        var slotSize = max - min;
        var fontSize = FontSizeFor(slotSize, slot, corner);
        var textSize = Measure(icon, fontSize);
        var pos = Position(min, max, corner, textSize, slot);
        drawList.AddText(UiBuilder.IconFont, fontSize, pos, color, icon.ToIconString());
    }

    public static float FontSizeFor(Vector2 slotSize, PanelSlot slot, SlotCorner corner) =>
        (Math.Max(8f, slotSize.Y * 0.26f) + 1f) * GetScale(slot, corner);

    private static float InsetFor(Vector2 slotSize, PanelSlot slot, SlotCorner corner) =>
        Math.Max(2f, slotSize.X * 0.08f) * GetScale(slot, corner);

    public static float GetScale(PanelSlot slot, SlotCorner corner)
    {
        var look = slot.GetCornerLook(corner);
        float scale;
        if (look.UseCustomScale)
            scale = look.Scale;
        else if (slot.BadgeUseCustomScale)
            scale = slot.BadgeScale;
        else
            scale = Config.CornerBadgeScale;

        return scale <= 0f ? 1f : Math.Clamp(scale, MinScale, MaxScale);
    }

    public static Vector2 Measure(FontAwesomeIcon icon, float fontSize)
    {
        using (ImRaii.PushFont(UiBuilder.IconFont))
            return ImGui.CalcTextSize(icon.ToIconString()) * (fontSize / ImGui.GetFontSize());
    }

    public static Vector2 Position(Vector2 min, Vector2 max, SlotCorner corner, Vector2 textSize, PanelSlot slot)
    {
        var inset = InsetFor(max - min, slot, corner);
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
        return value != 0 && Enum.IsDefined(icon);
    }
}
