using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using EnhancedQuickPanel.Models;
using EnhancedQuickPanel.Services;

namespace EnhancedQuickPanel.UI;

/// <summary>Inline outline editor shown in the slot editor body.</summary>
internal static class OutlineColorPicker
{
    private const string PickerPopupId = "##eqpOutlinePickerPopup";

    private static PanelSlot? _targetSlot;
    private static bool _isOpen;
    private static bool _draftEnabled;
    private static Vector4 _draftColor;
    private static bool _draftUseDefaultColor;
    private static float _draftThickness;
    private static bool _draftUseDefaultThickness;

    public static bool IsOpenFor(PanelSlot slot) =>
        _isOpen && ReferenceEquals(_targetSlot, slot);

    public static void Open(PanelSlot slot)
    {
        SlotOutlineDrawer.ClearLivePreview();
        _targetSlot = slot;
        _isOpen = true;
        _draftEnabled = slot.ShowOutline;
        _draftUseDefaultColor = !slot.OutlineUseCustomColor;
        _draftColor = SlotOutlineDrawer.GetColor(slot);
        _draftUseDefaultThickness = !slot.OutlineUseCustomThickness;
        _draftThickness = SlotOutlineDrawer.GetThickness(slot);
        PushLive(slot);
    }

    public static void Close()
    {
        if (_isOpen && _targetSlot != null)
            Commit(_targetSlot);

        _isOpen = false;
        _targetSlot = null;
        SlotOutlineDrawer.ClearLivePreview();
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
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var square = ImGui.GetFrameHeight();

        if (MirageUi.Checkbox(T("slot.outline.enable"), ref _draftEnabled))
            PushLive(slot);

        using (ImRaii.Disabled(!_draftEnabled))
        {
            var fieldWidth = Math.Max(
                28f,
                (ImGui.GetContentRegionAvail().X - square * 2f - spacing * 5f) / 4f);

            var colorChanged = DrawChannel("R", "##eqpOutlineR", ref _draftColor.X, fieldWidth);
            ImGui.SameLine(0f, spacing);
            colorChanged |= DrawChannel("G", "##eqpOutlineG", ref _draftColor.Y, fieldWidth);
            ImGui.SameLine(0f, spacing);
            colorChanged |= DrawChannel("B", "##eqpOutlineB", ref _draftColor.Z, fieldWidth);
            ImGui.SameLine(0f, spacing);
            colorChanged |= DrawChannel("A", "##eqpOutlineA", ref _draftColor.W, fieldWidth);

            ImGui.SameLine(0f, spacing);
            if (ImGui.ColorButton(
                    "##eqpOutlineSwatch",
                    _draftColor,
                    ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoTooltip,
                    new Vector2(square, square)))
                ImGui.OpenPopup(PickerPopupId);

            ImGui.SameLine(0f, spacing);
            DrawResetButton("##eqpOutlineColorReset", style, () =>
            {
                _draftColor = Config.SlotOutlineColor;
                _draftUseDefaultColor = true;
                colorChanged = false;
            });

            if (colorChanged)
                _draftUseDefaultColor = false;

            ImGui.SetNextItemWidth(Math.Max(32f, ImGui.GetContentRegionAvail().X - square - spacing));
            if (ImGui.SliderFloat(
                    "##eqpOutlineThickness",
                    ref _draftThickness,
                    SlotOutlineDrawer.MinThickness,
                    SlotOutlineDrawer.MaxThickness,
                    $"{T("slot.outline.thickness")}: %.1f"))
                _draftUseDefaultThickness = false;

            ImGui.SameLine(0f, spacing);
            DrawResetButton("##eqpOutlineThicknessReset", style, () =>
            {
                _draftThickness = Config.SlotOutlineThickness;
                _draftUseDefaultThickness = true;
            });
        }

        if (ImGui.BeginPopup(PickerPopupId))
        {
            if (ImGui.ColorPicker4("##eqpOutlinePickerColor", ref _draftColor))
                _draftUseDefaultColor = false;
            ImGui.EndPopup();
        }

        PushLive(slot);
    }

    private static void Commit(PanelSlot slot)
    {
        slot.ShowOutline = _draftEnabled;
        if (_draftUseDefaultColor)
            slot.OutlineUseCustomColor = false;
        else
            slot.SetOutlineColor(_draftColor);

        if (_draftUseDefaultThickness)
            slot.OutlineUseCustomThickness = false;
        else
            slot.SetOutlineThickness(_draftThickness);

        Config.Save();
    }

    private static void PushLive(PanelSlot slot) =>
        SlotOutlineDrawer.SetLivePreview(slot, _draftColor, _draftThickness, _draftEnabled);

    private static void DrawResetButton(string id, PanelUiStyleConfig style, Action onReset)
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
            ImGui.SetTooltip(T("slot.outline.resetDefault"));
    }

    private static bool DrawChannel(string label, string id, ref float value, float width)
    {
        var display = value * 255f;
        ImGui.SetNextItemWidth(width);
        var changed = ImGui.DragFloat(id, ref display, 1f, 0f, 255f, $"{label}:%.0f");
        if (changed)
            value = display / 255f;
        return changed;
    }
}
