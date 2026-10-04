using EnhancedQuickPanel.Models;

namespace EnhancedQuickPanel.Services;

/// <summary>Draws an optional colored outline around a slot. Off unless enabled.</summary>
internal static class SlotOutlineDrawer
{
    public const float MinThickness = 1f;
    public const float MaxThickness = 8f;

    private static PanelSlot? _liveSlot;
    private static Vector4 _liveColor;
    private static float _liveThickness;
    private static bool _liveEnabled;

    public static bool IsEnabled(PanelSlot slot) =>
        IsLive(slot) ? _liveEnabled : slot.ShowOutline;

    public static Vector4 GetColor(PanelSlot slot) =>
        IsLive(slot)
            ? _liveColor
            : slot.OutlineUseCustomColor ? slot.OutlineColor : Config.SlotOutlineColor;

    public static float GetThickness(PanelSlot slot)
    {
        var thickness = IsLive(slot)
            ? _liveThickness
            : slot.OutlineUseCustomThickness ? slot.OutlineThickness : Config.SlotOutlineThickness;
        return Math.Clamp(thickness, MinThickness, MaxThickness);
    }

    public static void SetLivePreview(PanelSlot slot, Vector4 color, float thickness, bool enabled)
    {
        _liveSlot = slot;
        _liveColor = color;
        _liveThickness = thickness;
        _liveEnabled = enabled;
    }

    public static void ClearLivePreview()
    {
        _liveSlot = null;
    }

    private static bool IsLive(PanelSlot slot) =>
        ReferenceEquals(_liveSlot, slot);

    public static void Draw(
        ImDrawListPtr drawList,
        Vector2 min,
        Vector2 max,
        PanelSlot slot,
        bool isGrayedOut)
    {
        if (!IsEnabled(slot))
            return;

        var color = GetColor(slot);
        if (isGrayedOut)
            color = new Vector4(color.X * 0.5f, color.Y * 0.5f, color.Z * 0.5f, color.W);

        var size = max - min;
        var rounding = Math.Clamp(size.X * 0.11f, 3f, 6f);
        var thickness = GetThickness(slot);
        var inset = thickness * 0.5f;
        drawList.AddRect(
            min + new Vector2(inset, inset),
            max - new Vector2(inset, inset),
            ImGui.ColorConvertFloat4ToU32(color),
            rounding,
            ImDrawFlags.RoundCornersAll,
            thickness);
    }
}
