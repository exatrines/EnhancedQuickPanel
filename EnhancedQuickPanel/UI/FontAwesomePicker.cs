using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using EnhancedQuickPanel.Models;
using EnhancedQuickPanel.Services;

namespace EnhancedQuickPanel.UI;

/// <summary>Inline corner-icon editor shown in the slot editor body.</summary>
internal static class FontAwesomePicker
{
    private const float CellSize = 36f;
    private const string PickerPopupId = "##eqpFaPickerPopup";

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

    public static void DrawHeaderIcon(PanelSlot slot, Vector2 size)
    {
        if (!IsOpenFor(slot))
            return;

        var iconLocked = IsIconLocked(slot);
        var savedBadge = slot.GetCornerBadge(_corner);
        if (iconLocked)
            _hoverIcon = null;
        else if (_hoverIcon is { } hover)
            slot.SetCornerBadge(_corner, (ushort)hover);

        SlotIconPicker.DrawPreview(slot, size);
        var previewMin = ImGui.GetItemRectMin();
        var previewMax = ImGui.GetItemRectMax();
        if (!iconLocked)
            slot.SetCornerBadge(_corner, savedBadge);
        DrawCornerButtons(slot, previewMin, previewMax, Config.PanelUi);
    }

    public static void DrawEmbedded(PanelSlot slot, bool expanded)
    {
        if (!IsOpenFor(slot))
            return;

        var style = Config.PanelUi;
        var iconLocked = IsIconLocked(slot);
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var frameH = ImGui.GetFrameHeight();
        var colorLabel = T("faPicker.colorLabel");
        var sizeLabel = T("faPicker.size");
        var labelWidth = Math.Max(ImGui.CalcTextSize(colorLabel).X, ImGui.CalcTextSize(sizeLabel).X);

        DrawColorRow(slot, style, colorLabel, labelWidth, frameH, spacing, expanded);
        DrawSizeRow(slot, style, sizeLabel, labelWidth, frameH, spacing);
        if (iconLocked)
        {
            _hoverIcon = null;
            DrawLockedNotice();
        }
        else
        {
            DrawSearchRow(slot, style, frameH, spacing);
            DrawIconGrid(slot, style);
        }
    }

    private static bool IsIconLocked(PanelSlot slot) =>
        _corner == SlotCorner.BottomRight
        && SlotCornerBadgeDrawer.TryGetLockedBottomRight(slot, out _);

    private static void DrawLockedNotice()
    {
        var avail = ImGui.GetContentRegionAvail();
        using var child = ImRaii.Child("##eqpFaPickerLocked", avail, false);
        if (!child)
            return;

        using (PanelUiTextStyle.PushText(Config.PanelUi))
            ImGui.TextWrapped(T("slot.corner.locked"));
    }

    private static void DrawColorRow(
        PanelSlot slot,
        PanelUiStyleConfig style,
        string label,
        float labelWidth,
        float frameH,
        float spacing,
        bool expanded)
    {
        DrawRowLabel(label, labelWidth, spacing);
        var color = SlotCornerBadgeDrawer.GetColor(slot, _corner);
        var avail = ImGui.GetContentRegionAvail().X;
        var fieldWidth = MathF.Floor((avail - frameH * 2f - spacing * 5f) / 4f);
        if (fieldWidth < 1f)
            fieldWidth = 1f;
        var colorChanged = DrawChannel($"##eqpFaPickerR{(int)_corner}", ref color.X, fieldWidth, expanded ? "R:%.0f" : "%.0f");
        ImGui.SameLine(0f, spacing);
        colorChanged |= DrawChannel($"##eqpFaPickerG{(int)_corner}", ref color.Y, fieldWidth, expanded ? "G:%.0f" : "%.0f");
        ImGui.SameLine(0f, spacing);
        colorChanged |= DrawChannel($"##eqpFaPickerB{(int)_corner}", ref color.Z, fieldWidth, expanded ? "B:%.0f" : "%.0f");
        ImGui.SameLine(0f, spacing);
        colorChanged |= DrawChannel($"##eqpFaPickerA{(int)_corner}", ref color.W, fieldWidth, expanded ? "A:%.0f" : "%.0f");

        ImGui.SameLine(0f, spacing);
        if (ImGui.ColorButton(
                "##eqpFaPickerSwatch",
                color,
                ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoTooltip,
                new Vector2(frameH, frameH)))
            ImGui.OpenPopup(PickerPopupId);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(T("faPicker.color"));

        ImGui.SameLine(0f, spacing);
        var reset = false;
        DrawResetButton("##eqpFaPickerColorReset", style, T("faPicker.useDefaultColor"), () =>
        {
            slot.ClearCornerColor(_corner);
            reset = true;
        });

        if (ImGui.BeginPopup(PickerPopupId))
        {
            if (ImGui.ColorPicker4("##eqpFaPickerColor", ref color))
                colorChanged = true;
            ImGui.EndPopup();
        }

        if (colorChanged)
            slot.SetCornerColor(_corner, color);

        if (colorChanged || reset)
            Config.Save();
    }

    private static void DrawSizeRow(
        PanelSlot slot,
        PanelUiStyleConfig style,
        string label,
        float labelWidth,
        float frameH,
        float spacing)
    {
        DrawRowLabel(label, labelWidth, spacing);
        var scale = SlotCornerBadgeDrawer.GetScale(slot, _corner);
        ImGui.SetNextItemWidth(Math.Max(32f, ImGui.GetContentRegionAvail().X - frameH - spacing));
        var changed = ImGui.SliderFloat(
            $"##eqpFaPickerScale{(int)_corner}",
            ref scale,
            SlotCornerBadgeDrawer.MinScale,
            SlotCornerBadgeDrawer.MaxScale,
            "%.2f");
        if (changed)
            slot.SetCornerScale(_corner, scale);

        ImGui.SameLine(0f, spacing);
        var reset = false;
        DrawResetButton("##eqpFaPickerScaleReset", style, T("slot.outline.resetDefault"), () =>
        {
            slot.ClearCornerScale(_corner);
            reset = true;
        });

        if (changed || reset)
            Config.Save();
    }

    private static void DrawSearchRow(
        PanelSlot slot,
        PanelUiStyleConfig style,
        float frameH,
        float spacing)
    {
        var search = _search;
        ImGui.SetNextItemWidth(Math.Max(32f, ImGui.GetContentRegionAvail().X - frameH - spacing));
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

    private static void DrawRowLabel(string label, float labelWidth, float spacing)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        ImGui.SameLine(labelWidth + spacing);
    }

    private static bool DrawChannel(string id, ref float value, float width, string format)
    {
        var display = value * 255f;
        ImGui.SetNextItemWidth(width);
        var changed = ImGui.DragFloat(id, ref display, 1f, 0f, 255f, format);
        if (changed)
            value = display / 255f;
        return changed;
    }

    private static void DrawResetButton(string id, PanelUiStyleConfig style, string tooltip, Action onReset)
    {
        using (new PanelUiButtonStyleScope(style))
        {
            if (CenteredIconButton.Draw(
                    FontAwesomeIcon.Undo,
                    id,
                    new Vector2(ImGui.GetFrameHeight(), ImGui.GetFrameHeight()),
                    style.TextColor,
                    style.TextHoverColor))
                onReset();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
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
                foreach (var corner in Enum.GetValues<SlotCorner>())
                    DrawCornerButton(slot, corner, slotSize, style);
            }
        }

        ImGui.SetCursorPos(restore);
    }

    private static void DrawCornerButton(
        PanelSlot slot,
        SlotCorner corner,
        Vector2 slotSize,
        PanelUiStyleConfig style)
    {
        var locked = false;
        if (corner == SlotCorner.BottomRight)
            locked = SlotCornerBadgeDrawer.TryGetLockedBottomRight(slot, out _);

        var hasBadge = SlotCornerBadgeDrawer.TryResolveIcon(slot.GetCornerBadge(corner), out _);
        var hoveringThis = !locked && corner == _corner && _hoverIcon != null;
        var previewShowsIcon = locked || hoveringThis || hasBadge;

        var fontSize = SlotCornerBadgeDrawer.FontSizeFor(slotSize, slot, corner);
        var buttonSize = new Vector2(fontSize, fontSize);
        var localPos = SlotCornerBadgeDrawer.Position(Vector2.Zero, slotSize, corner, buttonSize, slot);
        ImGui.SetCursorPos(localPos);
        if (ImGui.InvisibleButton($"##eqpSlotCorner{corner}", buttonSize))
            _corner = corner;

        var hovered = ImGui.IsItemHovered();
        var selected = corner == _corner;
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
            ImGui.SetTooltip(CornerLabel(corner));
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
