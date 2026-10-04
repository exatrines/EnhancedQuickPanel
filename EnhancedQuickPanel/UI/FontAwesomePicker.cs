using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using EnhancedQuickPanel.Models;
using EnhancedQuickPanel.Services;

namespace EnhancedQuickPanel.UI;

/// <summary>Inline corner-icon editor shown in the slot editor body.</summary>
internal static class FontAwesomePicker
{
    private const float CellSize = 36f;

    private static readonly FontAwesomeIcon[] AllIcons = Enum.GetValues<FontAwesomeIcon>()
        .Where(icon => (ushort)icon != 0)
        .Distinct()
        .OrderBy(icon => icon.ToString(), StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static PanelSlot? _targetSlot;
    private static SlotCorner _corner = SlotCorner.TopLeft;
    private static bool _isOpen;
    private static string _search = string.Empty;
    private static FontAwesomeIcon[] _visible = AllIcons;
    private static FontAwesomeIcon? _hoverIcon;

    public static bool IsOpenFor(PanelSlot slot) =>
        _isOpen && ReferenceEquals(_targetSlot, slot);

    public static void Open(PanelSlot slot)
    {
        _targetSlot = slot;
        _corner = SlotCorner.TopLeft;
        _search = string.Empty;
        _visible = AllIcons;
        _hoverIcon = null;
        _isOpen = true;
    }

    public static void Close()
    {
        _isOpen = false;
        _targetSlot = null;
        _hoverIcon = null;
    }

    public static void NotifySlot(PanelSlot slot)
    {
        if (_isOpen && !ReferenceEquals(_targetSlot, slot))
            Close();
    }

    public static void DrawEmbedded(PanelSlot slot)
    {
        if (!IsOpenFor(slot))
            return;

        var style = Config.PanelUi;
        if (SlotCornerBadgeDrawer.TryGetLockedBottomRight(slot, out _, out _)
            && _corner == SlotCorner.BottomRight)
            _corner = SlotCorner.TopLeft;
        var spacing = ImGui.GetStyle().ItemSpacing;
        var frameH = ImGui.GetFrameHeight();
        var previewSize = frameH * 2f + spacing.Y;
        var savedBadge = slot.GetCornerBadge(_corner);
        if (_hoverIcon is { } hover)
            slot.SetCornerBadge(_corner, (ushort)hover);

        ImGui.BeginGroup();
        SlotIconPicker.DrawPreview(slot, new Vector2(previewSize, previewSize));
        var previewMin = ImGui.GetItemRectMin();
        var previewMax = ImGui.GetItemRectMax();
        slot.SetCornerBadge(_corner, savedBadge);
        DrawCornerButtons(slot, previewMin, previewMax, style);
        ImGui.EndGroup();

        ImGui.SameLine(0f, spacing.X);
        var rightWidth = Math.Max(32f, ImGui.GetContentRegionAvail().X);
        ImGui.BeginGroup();
        DrawColorRow(slot, style, rightWidth, frameH, spacing.X);
        DrawSearchRow(slot, style, rightWidth, frameH, spacing.X);
        ImGui.EndGroup();

        DrawIconGrid(slot, style);
    }

    private static void DrawColorRow(
        PanelSlot slot,
        PanelUiStyleConfig style,
        float width,
        float frameH,
        float spacing)
    {
        var color = SlotCornerBadgeDrawer.GetColor(slot);
        ImGui.SetNextItemWidth(Math.Max(32f, width - frameH - spacing));
        if (ImGui.ColorEdit4(
                "##eqpFaPickerColor",
                ref color,
                ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.NoLabel | ImGuiColorEditFlags.NoOptions))
        {
            slot.SetBadgeColor(color);
            Config.Save();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(T("faPicker.color"));

        ImGui.SameLine(0f, spacing);
        using (new PanelUiButtonStyleScope(style))
        {
            if (CenteredIconButton.Draw(
                    FontAwesomeIcon.Undo,
                    "##eqpFaPickerColorReset",
                    new Vector2(frameH, frameH),
                    style.TextColor,
                    style.TextHoverColor))
            {
                slot.BadgeUseCustomColor = false;
                Config.Save();
            }
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(T("faPicker.useDefaultColor"));
    }

    private static void DrawSearchRow(
        PanelSlot slot,
        PanelUiStyleConfig style,
        float width,
        float frameH,
        float spacing)
    {
        var search = _search;
        ImGui.SetNextItemWidth(Math.Max(32f, width - frameH - spacing));
        if (ImGui.InputTextWithHint("##eqpFaPickerSearch", T("faPicker.search"), ref search, 64))
        {
            _search = search;
            RefreshVisible();
        }

        ImGui.SameLine(0f, spacing);
        using (new PanelUiButtonStyleScope(style))
        {
            if (CenteredIconButton.Draw(
                    FontAwesomeIcon.Ban,
                    "##eqpFaPickerClear",
                    new Vector2(frameH, frameH),
                    style.TextColor,
                    style.TextHoverColor))
                Apply(slot, 0);
        }
    }

    private static void DrawCornerButtons(
        PanelSlot slot,
        Vector2 previewMin,
        Vector2 previewMax,
        PanelUiStyleConfig style)
    {
        var restore = ImGui.GetCursorPos();
        var slotSize = previewMax - previewMin;
        if (slotSize.X < 8f || slotSize.Y < 8f)
            return;

        var fontSize = SlotCornerBadgeDrawer.FontSizeFor(slotSize);
        ImGui.SetCursorScreenPos(previewMin);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
        using (ImRaii.PushColor(ImGuiCol.ChildBg, 0u))
        using (var overlay = ImRaii.Child(
                   "##eqpCornerPickers",
                   slotSize,
                   false,
                   ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            if (overlay)
            {
                DrawCornerButton(slot, SlotCorner.TopLeft, slotSize, fontSize, style);
                DrawCornerButton(slot, SlotCorner.TopRight, slotSize, fontSize, style);
                DrawCornerButton(slot, SlotCorner.BottomLeft, slotSize, fontSize, style);
                DrawCornerButton(slot, SlotCorner.BottomRight, slotSize, fontSize, style);
            }
        }

        ImGui.SetCursorPos(restore);
    }

    private static void DrawCornerButton(
        PanelSlot slot,
        SlotCorner corner,
        Vector2 slotSize,
        float fontSize,
        PanelUiStyleConfig style)
    {
        var locked = false;
        var lockedTooltipKey = string.Empty;
        if (corner == SlotCorner.BottomRight)
            locked = SlotCornerBadgeDrawer.TryGetLockedBottomRight(slot, out _, out lockedTooltipKey);

        var hasBadge = SlotCornerBadgeDrawer.TryResolveIcon(slot.GetCornerBadge(corner), out _);
        var hoveringThis = !locked && corner == _corner && _hoverIcon != null;
        var previewShowsIcon = locked || hoveringThis || hasBadge;

        var buttonSize = new Vector2(fontSize, fontSize);
        var localPos = SlotCornerBadgeDrawer.Position(Vector2.Zero, slotSize, corner, buttonSize);
        ImGui.SetCursorPos(localPos);
        if (ImGui.InvisibleButton($"##eqpSlotCorner{corner}", buttonSize) && !locked)
            _corner = corner;

        var hovered = ImGui.IsItemHovered();
        var selected = !locked && corner == _corner;
        var screenPos = ImGui.GetItemRectMin();
        var drawList = ImGui.GetWindowDrawList();
        if (selected)
        {
            var pad = new Vector2(2f, 2f);
            drawList.AddRect(
                screenPos - pad,
                screenPos + buttonSize + pad,
                ImGui.ColorConvertFloat4ToU32(MirageUi.GetColor(MirageUi.Color.Accent)),
                2f,
                ImDrawFlags.None,
                2f);
        }

        if (!previewShowsIcon)
        {
            var glyph = SlotCornerBadgeDrawer.Measure(FontAwesomeIcon.Plus, fontSize);
            var textPos = screenPos + (buttonSize - glyph) * 0.5f;
            var color = selected ? MirageUi.GetColor(MirageUi.Color.Accent) : style.TextColor;
            drawList.AddText(
                UiBuilder.IconFont,
                fontSize,
                textPos,
                ImGui.ColorConvertFloat4ToU32(color),
                FontAwesomeIcon.Plus.ToIconString());
        }

        if (hovered)
            ImGui.SetTooltip(locked ? T(lockedTooltipKey) : CornerLabel(corner));
    }

    private static void DrawIconGrid(PanelSlot slot, PanelUiStyleConfig style)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var avail = ImGui.GetContentRegionAvail();
        var columns = Math.Max(1, (int)((avail.X + spacing) / (CellSize + spacing)));
        var gridHeight = Math.Max(CellSize, avail.Y);
        using var child = ImRaii.Child("##eqpFaPickerGrid", new Vector2(avail.X, gridHeight), false);
        if (!child)
            return;

        _hoverIcon = null;
        var selected = slot.GetCornerBadge(_corner);
        using (new PanelUiButtonStyleScope(style))
        {
            for (var i = 0; i < _visible.Length; i++)
            {
                if (i % columns != 0)
                    ImGui.SameLine(0f, spacing);

                var icon = _visible[i];
                var id = $"##eqpFaPick{(ushort)icon}";
                if (CenteredIconButton.Draw(icon, id, new Vector2(CellSize, CellSize)))
                    Apply(slot, (ushort)icon);

                if (ImGui.IsItemHovered())
                {
                    _hoverIcon = icon;
                    ImGui.SetTooltip(icon.ToString());
                }

                if ((ushort)icon == selected)
                {
                    var min = ImGui.GetItemRectMin();
                    var max = ImGui.GetItemRectMax();
                    ImGui.GetWindowDrawList().AddRect(min, max, ImGui.GetColorU32(ImGuiCol.Text), 3f, ImDrawFlags.None, 2f);
                }
            }
        }
    }

    private static void Apply(PanelSlot slot, ushort value)
    {
        slot.SetCornerBadge(_corner, value);
        Config.Save();
    }

    private static void RefreshVisible()
    {
        if (string.IsNullOrWhiteSpace(_search))
        {
            _visible = AllIcons;
            return;
        }

        var query = _search.Trim();
        _visible = AllIcons
            .Where(icon => icon.ToString().Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static string CornerLabel(SlotCorner corner) =>
        corner switch
        {
            SlotCorner.TopLeft => T("slot.corner.topLeft"),
            SlotCorner.TopRight => T("slot.corner.topRight"),
            SlotCorner.BottomLeft => T("slot.corner.bottomLeft"),
            _ => T("slot.corner.bottomRight"),
        };
}
