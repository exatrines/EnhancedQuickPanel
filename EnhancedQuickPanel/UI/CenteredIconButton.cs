using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using EnhancedQuickPanel.Models;
using System.Numerics;

namespace EnhancedQuickPanel.UI;

/// <summary>Draws an icon button with its glyph centered.</summary>
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

    internal static float ComputeIconY(float cursorY, float buttonHeight, float iconHeight)
    {
        var contentHeight = buttonHeight - IconPaddingTop - IconPaddingBottom;
        return cursorY + IconPaddingTop + (contentHeight - iconHeight) * 0.5f;
    }
}

/// <summary>Draws a text input field prefixed with a left-aligned icon.</summary>
internal static class LeftAlignedIconInputField
{
    public static (bool IconClicked, bool Hovered) Draw(
        FontAwesomeIcon icon,
        string inputId,
        ref string value,
        PanelUiStyleConfig style,
        float width,
        int maxLength = 64,
        string hint = "")
    {
        style.EnsureDefaults();

        var framePadding = ImGui.GetStyle().FramePadding;
        var iconPadding = 3f * ImGuiHelpers.GlobalScale;
        var buttonHeight = ImGui.GetFrameHeight();
        var cursor = ImGui.GetCursorScreenPos();
        var size = new Vector2(width, buttonHeight);

        var iconText = icon.ToIconString();
        Vector2 iconSize;
        using (ImRaii.PushFont(UiBuilder.IconFont))
            iconSize = ImGui.CalcTextSize(iconText);

        var iconAreaWidth = framePadding.X + iconSize.X + iconPadding;
        var inputWidth = Math.Max(32f, width - iconAreaWidth - framePadding.X);
        var hovered = ImGui.IsMouseHoveringRect(cursor, cursor + size, false);

        var bgColor = hovered ? style.ButtonBgHoverColor : style.ButtonBgColor;
        var textColor = hovered ? style.TextHoverColor : style.TextColor;

        ImGui.BeginGroup();
        ImGui.Dummy(size);

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(
            cursor,
            cursor + size,
            ImGui.ColorConvertFloat4ToU32(bgColor),
            ImGui.GetStyle().FrameRounding);

        var iconPos = new Vector2(
            cursor.X + framePadding.X,
            CenteredIconButton.ComputeIconY(cursor.Y, buttonHeight, iconSize.Y));
        using (ImRaii.PushFont(UiBuilder.IconFont))
            drawList.AddText(iconPos, ImGui.ColorConvertFloat4ToU32(textColor), iconText);

        ImGui.SetCursorScreenPos(new Vector2(cursor.X + iconAreaWidth, cursor.Y));
        ImGui.SetNextItemWidth(inputWidth);

        using (new PanelUiEditFieldStyleScope(style))
        using (PanelUiTextStyle.PushInputText(style, inputId))
        {
            if (string.IsNullOrEmpty(hint))
                ImGui.InputTextWithHint(inputId, string.Empty, ref value, maxLength);
            else
                ImGui.InputTextWithHint(inputId, hint, ref value, maxLength);

            PanelUiTextStyle.NotifyInputHover(inputId);
        }

        ImGui.SetCursorScreenPos(cursor);
        var iconClicked = false;
        using (ImRaii.PushId($"{inputId}/icon"))
            iconClicked = ImGui.InvisibleButton("##icon", new Vector2(iconAreaWidth, buttonHeight));

        ImGui.EndGroup();

        return (iconClicked, hovered);
    }
}

/// <summary>Draws a button with a left-aligned icon followed by text.</summary>
internal static class LeftAlignedIconTextButton
{
    public static bool Draw(
        FontAwesomeIcon icon,
        string id,
        Vector2? size = null,
        bool enabled = true,
        bool scaling = true)
    {
        var textColor = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        return Draw(icon, id, textColor, textColor, size, enabled, scaling);
    }

    public static bool Draw(
        FontAwesomeIcon icon,
        string id,
        Vector4 textColor,
        Vector4 hoveredTextColor,
        Vector2? size = null,
        bool enabled = true,
        bool scaling = true)
    {
        if (!enabled)
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * 0.6f);

        if (size.HasValue && scaling)
            size *= ImGuiHelpers.GlobalScale;

        var iconText = icon.ToIconString();
        Vector2 iconSize;
        using (ImRaii.PushFont(UiBuilder.IconFont))
            iconSize = ImGui.CalcTextSize(iconText);

        var textStr = id;
        var hashIndex = textStr.IndexOf('#', StringComparison.Ordinal);
        if (hashIndex >= 0)
            textStr = textStr[..hashIndex];

        var framePadding = ImGui.GetStyle().FramePadding;
        var iconPadding = 3f * ImGuiHelpers.GlobalScale;
        var cursor = ImGui.GetCursorScreenPos();

        var textSize = ImGui.CalcTextSize(textStr);
        var buttonWidth = size is { X: > 0 }
            ? size.Value.X
            : iconSize.X + textSize.X + framePadding.X * 2f + iconPadding;
        var buttonHeight = size is { Y: > 0 } ? size.Value.Y : ImGui.GetFrameHeight();

        bool clicked;
        using (ImRaii.PushId(id))
            clicked = ImGui.Button(string.Empty, new Vector2(buttonWidth, buttonHeight));

        var isHovered = enabled && ImGui.IsItemHovered();
        var color = ImGui.ColorConvertFloat4ToU32(isHovered ? hoveredTextColor : textColor);

        var iconPos = new Vector2(
            cursor.X + framePadding.X,
            CenteredIconButton.ComputeIconY(cursor.Y, buttonHeight, iconSize.Y));
        var textPos = new Vector2(
            iconPos.X + iconSize.X + iconPadding,
            cursor.Y + (buttonHeight - textSize.Y) * 0.5f);

        var drawList = ImGui.GetWindowDrawList();
        using (ImRaii.PushFont(UiBuilder.IconFont))
            drawList.AddText(iconPos, color, iconText);
        drawList.AddText(textPos, color, textStr);

        if (!enabled)
            ImGui.PopStyleVar();

        return clicked && enabled;
    }
}

