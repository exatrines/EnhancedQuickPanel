using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using EnhancedQuickPanel.Models;

namespace EnhancedQuickPanel.UI;

/// <summary>Applies the configured dropdown colors within a using scope.</summary>
internal readonly struct PanelUiDropdownStyleScope : IDisposable
{
    private readonly ImRaii.ColorDisposable? _windowBg;
    private readonly ImRaii.ColorDisposable? _popupBg;

    public PanelUiDropdownStyleScope(PanelUiStyleConfig style)
    {
        style.EnsureDefaults();
        var dropdownBg = style.DropdownBgColor;
        _windowBg = ImRaii.PushColor(ImGuiCol.WindowBg, dropdownBg);
        _popupBg = ImRaii.PushColor(ImGuiCol.PopupBg, dropdownBg);
    }

    public void Dispose()
    {
        _popupBg?.Dispose();
        _windowBg?.Dispose();
    }
}

/// <summary>Applies the configured edit-field colors within a using scope.</summary>
internal readonly struct PanelUiEditFieldStyleScope : IDisposable
{
    private readonly ImRaii.ColorDisposable? _frameBg;
    private readonly ImRaii.ColorDisposable? _frameBgHovered;
    private readonly ImRaii.ColorDisposable? _frameBgActive;
    private readonly ImRaii.ColorDisposable? _textDisabled;
    private readonly ImRaii.ColorDisposable? _popupBg;

    public PanelUiEditFieldStyleScope(PanelUiStyleConfig style)
    {
        style.EnsureDefaults();
        var fieldBg = style.FieldBgColor;
        _frameBg = ImRaii.PushColor(ImGuiCol.FrameBg, fieldBg);
        _frameBgHovered = ImRaii.PushColor(ImGuiCol.FrameBgHovered, fieldBg);
        _frameBgActive = ImRaii.PushColor(ImGuiCol.FrameBgActive, fieldBg);
        _textDisabled = ImRaii.PushColor(ImGuiCol.TextDisabled, style.PlaceholderTextColor);
        _popupBg = ImRaii.PushColor(ImGuiCol.PopupBg, style.DropdownBgColor);
    }

    public void Dispose()
    {
        _popupBg?.Dispose();
        _textDisabled?.Dispose();
        _frameBgActive?.Dispose();
        _frameBgHovered?.Dispose();
        _frameBg?.Dispose();
    }
}

/// <summary>Applies the configured panel-UI button colors within a using scope.</summary>
internal readonly struct PanelUiButtonStyleScope : IDisposable
{
    private readonly ImRaii.ColorDisposable? _button;
    private readonly ImRaii.ColorDisposable? _buttonHovered;
    private readonly ImRaii.ColorDisposable? _buttonActive;

    public PanelUiButtonStyleScope(PanelUiStyleConfig style)
    {
        _button = ImRaii.PushColor(ImGuiCol.Button, style.ButtonBgColor);
        _buttonHovered = ImRaii.PushColor(ImGuiCol.ButtonHovered, style.ButtonBgHoverColor);
        _buttonActive = ImRaii.PushColor(ImGuiCol.ButtonActive, style.ButtonBgHoverColor);
    }

    public void Dispose()
    {
        _buttonActive?.Dispose();
        _buttonHovered?.Dispose();
        _button?.Dispose();
    }
}

/// <summary>Applies the configured context-menu colors within a using scope.</summary>
internal readonly struct ContextMenuStyleScope : IDisposable
{
    private readonly ImRaii.ColorDisposable? _popupBg;
    private readonly ImRaii.ColorDisposable? _windowBg;
    private readonly ImRaii.ColorDisposable? _childBg;
    private readonly ImRaii.StyleDisposable? _windowPadding;
    private readonly ImRaii.StyleDisposable? _itemSpacing;
    private readonly ImRaii.StyleDisposable? _windowBorderSize;

    public ContextMenuStyleScope(ContextMenuStyleConfig style)
    {
        style.EnsureDefaults();
        var padding = style.Padding;
        var bg = style.BgColor;
        _popupBg = ImRaii.PushColor(ImGuiCol.PopupBg, bg);
        _windowBg = ImRaii.PushColor(ImGuiCol.WindowBg, bg);
        _childBg = ImRaii.PushColor(ImGuiCol.ChildBg, bg);
        _windowPadding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(padding, padding));
        _itemSpacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        _windowBorderSize = ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 0f);
    }

    public void Dispose()
    {
        _windowBorderSize?.Dispose();
        _itemSpacing?.Dispose();
        _windowPadding?.Dispose();
        _childBg?.Dispose();
        _windowBg?.Dispose();
        _popupBg?.Dispose();
    }
}

/// <summary>Helpers for drawing a single context-menu entry with consistent styling.</summary>
internal static class ContextMenuItem
{
    private const float IconTextGap = 6f;

    public static float ComputeRequiredWidth(ContextMenuStyleConfig style, ReadOnlySpan<string> labels)
    {
        style.EnsureDefaults();
        var padding = style.Padding;
        var maxText = 0f;
        foreach (var label in labels)
            maxText = Math.Max(maxText, ImGui.CalcTextSize(label).X);

        var checkWidth = ImGui.CalcTextSize("✓").X;
        Vector2 iconSize;
        using (ImRaii.PushFont(UiBuilder.IconFont))
            iconSize = ImGui.CalcTextSize(FontAwesomeIcon.Clipboard.ToIconString());

        var minWidth = padding * 2f + iconSize.X + IconTextGap + maxText + checkWidth;
        return Math.Max(style.Width, minWidth);
    }

    public static float ComputeRequiredWidth(ContextMenuStyleConfig style, params string[] labels) =>
        ComputeRequiredWidth(style, labels.AsSpan());

    public static float ComputeRowHeight(ContextMenuStyleConfig style, ReadOnlySpan<string> labels)
    {
        style.EnsureDefaults();
        var padding = style.Padding;
        var textHeight = 0f;
        foreach (var label in labels)
            textHeight = Math.Max(textHeight, ImGui.CalcTextSize(label).Y);

        textHeight = Math.Max(textHeight, ImGui.CalcTextSize("✓").Y);
        Vector2 iconSize;
        using (ImRaii.PushFont(UiBuilder.IconFont))
            iconSize = ImGui.CalcTextSize(FontAwesomeIcon.Clipboard.ToIconString());

        return Math.Max(textHeight, iconSize.Y) + padding * 2f;
    }

    public static float ComputeRowHeight(ContextMenuStyleConfig style, params string[] labels) =>
        ComputeRowHeight(style, labels.AsSpan());

    public static bool Draw(
        string label,
        FontAwesomeIcon icon,
        string id,
        ContextMenuStyleConfig style,
        string? trailingLabel = null,
        float indent = 0f)
    {
        style.EnsureDefaults();
        if (indent > 0f)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + indent);

        var padding = new Vector2(style.Padding, style.Padding);
        var textSize = ImGui.CalcTextSize(label);
        var trailingSize = string.IsNullOrEmpty(trailingLabel)
            ? Vector2.Zero
            : ImGui.CalcTextSize(trailingLabel);
        var iconText = icon.ToIconString();
        Vector2 iconSize;
        using (ImRaii.PushFont(UiBuilder.IconFont))
            iconSize = ImGui.CalcTextSize(iconText);

        var contentHeight = Math.Max(textSize.Y, iconSize.Y);
        if (!string.IsNullOrEmpty(trailingLabel))
            contentHeight = Math.Max(contentHeight, trailingSize.Y);

        var itemWidth = ImGui.GetContentRegionAvail().X;
        if (itemWidth <= 0f)
            itemWidth = Math.Max(0f, style.Width - style.Padding * 2f);
        var itemHeight = contentHeight + padding.Y * 2f;

        using (ImRaii.PushId(id))
        using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, padding))
        using (ImRaii.PushColor(ImGuiCol.Header, style.ButtonBgColor))
        using (ImRaii.PushColor(ImGuiCol.HeaderHovered, style.ButtonBgHoverColor))
        using (ImRaii.PushColor(ImGuiCol.HeaderActive, style.ButtonBgHoverColor))
        {
            var clicked = ImGui.Selectable(
                "##eqpContextMenuItem",
                false,
                ImGuiSelectableFlags.None,
                new Vector2(itemWidth, itemHeight));

            var hovered = ImGui.IsItemHovered();
            var textColor = hovered ? style.TextHoverColor : style.TextColor;
            var color = ImGui.ColorConvertFloat4ToU32(textColor);
            var rectMin = ImGui.GetItemRectMin();
            var rectMax = ImGui.GetItemRectMax();
            var rowHeight = rectMax.Y - rectMin.Y;
            var iconPos = new Vector2(
                rectMin.X + padding.X,
                rectMin.Y + (rowHeight - iconSize.Y) * 0.5f);
            var textPos = new Vector2(
                iconPos.X + iconSize.X + IconTextGap,
                rectMin.Y + (rowHeight - textSize.Y) * 0.5f);

            var drawList = ImGui.GetWindowDrawList();
            using (ImRaii.PushFont(UiBuilder.IconFont))
                drawList.AddText(iconPos, color, iconText);
            drawList.AddText(textPos, color, label);

            if (!string.IsNullOrEmpty(trailingLabel))
            {
                var trailingPos = new Vector2(
                    rectMax.X - padding.X - trailingSize.X,
                    rectMin.Y + (rowHeight - trailingSize.Y) * 0.5f);
                drawList.AddText(trailingPos, color, trailingLabel);
            }

            return clicked;
        }
    }
}

/// <summary>Helpers for drawing panel-UI text with the configured colors.</summary>
internal static class PanelUiTextStyle
{
    private static uint _hoveredInputId;

    public static ImRaii.ColorDisposable PushText(PanelUiStyleConfig style) =>
        ImRaii.PushColor(ImGuiCol.Text, style.TextColor);

    public static ImRaii.ColorDisposable PushTextDisabled(PanelUiStyleConfig style)
    {
        style.EnsureDefaults();
        return ImRaii.PushColor(ImGuiCol.TextDisabled, style.PlaceholderTextColor);
    }

    public static IDisposable PushInputText(PanelUiStyleConfig style, string id)
    {
        var itemId = ImGui.GetID(id);
        var textColor = itemId == _hoveredInputId ? style.TextHoverColor : style.TextColor;
        return ImRaii.PushColor(ImGuiCol.Text, textColor);
    }

    public static void NotifyInputHover(string label)
    {
        var itemId = ImGui.GetID(label);
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem))
            _hoveredInputId = itemId;
        else if (!ImGui.IsItemActive() && _hoveredInputId == itemId)
            _hoveredInputId = 0;
    }
}
