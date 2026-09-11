using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace EnhancedQuickPanel.UI;

// Icon-only button with a centered Font Awesome glyph.
internal static class CenteredIconButton
{
    private const float IconPaddingTop = -1f;
    private const float IconPaddingBottom = 1f;

    public static bool Draw(FontAwesomeIcon icon, string id, Vector2 size, bool enabled = true)
    {
        var textColor = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        return Draw(icon, id, size, textColor, textColor, enabled);
    }

    public static bool Draw(
        FontAwesomeIcon icon,
        string id,
        Vector2 size,
        Vector4 textColor,
        Vector4 hoveredTextColor,
        bool enabled = true,
        string? disabledTooltip = null)
    {
        if (!enabled)
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * 0.6f);

        var buttonWidth = size.X > 0f ? size.X : ImGui.GetFrameHeight();
        var buttonHeight = size.Y > 0f ? size.Y : ImGui.GetFrameHeight();

        var cursor = ImGui.GetCursorScreenPos();

        bool clicked;
        using (ImRaii.PushId(id))
            clicked = ImGui.Button(string.Empty, new Vector2(buttonWidth, buttonHeight));

        var hovered = ImGui.IsItemHovered();
        if (!enabled && hovered && !string.IsNullOrEmpty(disabledTooltip))
            ImGui.SetTooltip(disabledTooltip);

        var isHovered = enabled && hovered;
        var color = ImGui.ColorConvertFloat4ToU32(isHovered ? hoveredTextColor : textColor);

        Vector2 iconSize;
        var iconText = icon.ToIconString();
        using (ImRaii.PushFont(UiBuilder.IconFont))
            iconSize = ImGui.CalcTextSize(iconText);

        var iconPos = new Vector2(
            cursor.X + (buttonWidth - iconSize.X) * 0.5f,
            ComputeIconY(cursor.Y, buttonHeight, iconSize.Y));

        var drawList = ImGui.GetWindowDrawList();
        using (ImRaii.PushFont(UiBuilder.IconFont))
            drawList.AddText(iconPos, color, iconText);

        if (!enabled)
            ImGui.PopStyleVar();

        return clicked && enabled;
    }

    private static float ComputeIconY(float cursorY, float buttonHeight, float iconHeight)
    {
        var contentHeight = buttonHeight - IconPaddingTop - IconPaddingBottom;
        return cursorY + IconPaddingTop + (contentHeight - iconHeight) * 0.5f;
    }
}
