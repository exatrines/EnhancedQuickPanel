using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ECommons.ImGuiMethods;
using EnhancedQuickPanel.Models;
using EnhancedQuickPanel.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace EnhancedQuickPanel.UI;

/// <summary>UI for editing a single slot's kind, command, icon, and text content.</summary>
internal static class SlotEditor
{
    private static bool UnusedSlotEditorExpanded;

    private static byte _previewMacroSet = byte.MaxValue;
    private static byte _previewMacroIndex = byte.MaxValue;
    private static bool _previewAvailable;
    private static string _previewStatusMessage = string.Empty;
    private static string _previewDisplayText = string.Empty;
    private static string _previewMacroBodyText = string.Empty;

    private static readonly (RaptureHotbarModule.HotbarSlotType Type, string Label)[] CommonSlotTypes =
    [
        (RaptureHotbarModule.HotbarSlotType.Action, "Action"),
        (RaptureHotbarModule.HotbarSlotType.GeneralAction, "General Action"),
        (RaptureHotbarModule.HotbarSlotType.Item, "Item"),
        (RaptureHotbarModule.HotbarSlotType.CraftAction, "Craft Action"),
        (RaptureHotbarModule.HotbarSlotType.Emote, "Emote"),
        (RaptureHotbarModule.HotbarSlotType.Mount, "Mount"),
        (RaptureHotbarModule.HotbarSlotType.Macro, "Macro"),
        (RaptureHotbarModule.HotbarSlotType.MainCommand, "Main Command"),
        (RaptureHotbarModule.HotbarSlotType.GearSet, "Gear Set"),
        (RaptureHotbarModule.HotbarSlotType.Companion, "Companion"),
        (RaptureHotbarModule.HotbarSlotType.BuddyAction, "Buddy Action"),
        (RaptureHotbarModule.HotbarSlotType.PetAction, "Pet Action"),
        (RaptureHotbarModule.HotbarSlotType.Marker, "Marker"),
        (RaptureHotbarModule.HotbarSlotType.FieldMarker, "Field Marker"),
    ];

    public static void Draw(PanelSlot slot, bool overlayPanel = false) =>
        Draw(slot, overlayPanel, ref UnusedSlotEditorExpanded);

    public static void Draw(PanelSlot slot, bool overlayPanel, ref bool slotEditorExpanded)
    {
        if (overlayPanel)
        {
            DrawOverlayHeaderBar(slot, ref slotEditorExpanded);
            if (IsTextCommandEditorSlot(slot))
                DrawOverlayTextArea(slot);
            else if (slot.Kind == PanelSlotKind.Macro)
                DrawOverlayMacroContent(slot);
            return;
        }

        DrawStandardHeader(slot);

        switch (slot.Kind)
        {
            case PanelSlotKind.Action:
                DrawActionFields(slot);
                break;
            case PanelSlotKind.Macro:
                DrawMacroFields(slot);
                break;
            case PanelSlotKind.TextCommand:
                DrawTextCommandFields(slot, overlayPanel: false);
                break;
        }

        if (slot.IsConfigured && ImGui.Button(T("common.testExecute")))
            Services.SlotExecutor.Execute(slot);
    }

    private static void DrawStandardHeader(PanelSlot slot)
    {
        DrawKindCombo(slot);
        DrawLabelAndIcon(slot);
    }

    private static void DrawOverlayHeaderBar(PanelSlot slot, ref bool slotEditorExpanded)
    {
        var style = C.PanelUi;
        var nameEditable = IsTextCommandEditorSlot(slot);
        var blockHeight = ImGui.GetFrameHeight() * 2f + ImGui.GetStyle().ItemSpacing.Y;
        var iconButtonSize = new Vector2(blockHeight, blockHeight);
        var actionButtonSize = new Vector2(ImGui.GetFrameHeight(), ImGui.GetFrameHeight());
        var spacing = ImGui.GetStyle().ItemSpacing.X;

        using (new PanelUiEditFieldStyleScope(style))
        {
            SlotIconPicker.DrawIconButton(slot, iconButtonSize, interactive: nameEditable);
            ImGui.SameLine(0f, spacing);

            var contentWidth = ImGui.GetContentRegionAvail().X;
            var nameRowWidth = Math.Max(
                32f,
                contentWidth - actionButtonSize.X * 2f - spacing * 2f);

            ImGui.BeginGroup();

            var kindRowStartX = ImGui.GetCursorPosX();
            ImGui.TextUnformatted(ResolveOverlayKindLabel(slot));
            ImGui.SameLine();
            ImGui.SetCursorPosX(kindRowStartX + contentWidth - actionButtonSize.X);
            using (new PanelUiButtonStyleScope(style))
            {
                var expandIcon = slotEditorExpanded
                    ? FontAwesomeIcon.AngleDoubleLeft
                    : FontAwesomeIcon.AngleDoubleRight;
                if (CenteredIconButton.Draw(
                        expandIcon,
                        "##eqpOverlayExpand",
                        actionButtonSize,
                        style.TextColor,
                        style.TextHoverColor))
                    slotEditorExpanded = !slotEditorExpanded;
            }

            if (nameEditable)
            {
                var label = slot.Label;
                ImGui.PushItemWidth(nameRowWidth);
                using (PanelUiTextStyle.PushInputText(style, "##eqpOverlayName"))
                {
                    if (ImGui.InputTextWithHint("##eqpOverlayName", T("common.name"), ref label, 64))
                    {
                        slot.Label = label;
                        EzConfig.Save();
                    }

                    PanelUiTextStyle.NotifyInputHover("##eqpOverlayName");
                }

                ImGui.PopItemWidth();
            }
            else
            {
                ImGui.PushItemWidth(nameRowWidth);
                using (ImRaii.Disabled())
                using (PanelUiTextStyle.PushTextDisabled(style))
                {
                    var displayName = ResolveOverlayDisplayName(slot);
                    ImGui.InputTextWithHint("##eqpOverlayName", T("common.name"), ref displayName, 64, ImGuiInputTextFlags.ReadOnly);
                }

                ImGui.PopItemWidth();
            }

            ImGui.SameLine(0f, spacing);
            using (new PanelUiButtonStyleScope(style))
            {
                var canExecute = slot.IsConfigured;
                if (CenteredIconButton.Draw(
                        FontAwesomeIcon.Play,
                        "##eqpOverlayExecute",
                        actionButtonSize,
                        style.TextColor,
                        style.TextHoverColor,
                        enabled: canExecute)
                    && canExecute)
                    SlotExecutor.Execute(slot);

                ImGui.SameLine(0f, spacing);
                var ctrlHeld = ImGui.GetIO().KeyCtrl;
                if (CenteredIconButton.Draw(
                        FontAwesomeIcon.Trash,
                        "##eqpOverlayClear",
                        actionButtonSize,
                        style.TextColor,
                        style.TextHoverColor,
                        enabled: ctrlHeld,
                        disabledTooltip: T("common.deleteHint"))
                    && ctrlHeld)
                    ClearSlotContents(slot);
            }

            ImGui.EndGroup();
        }
    }

    private static void DrawOverlayTextArea(PanelSlot slot)
    {
        ImGui.Spacing();

        var style = C.PanelUi;
        var text = slot.TextBody;
        var textAreaHeight = Math.Max(80f, ImGui.GetContentRegionAvail().Y);
        using (new PanelUiEditFieldStyleScope(style))
        using (PanelUiTextStyle.PushInputText(style, "##eqpOverlayTextBody"))
        {
            if (ImGui.InputTextMultiline("##eqpOverlayTextBody", ref text, 4096, new Vector2(-1f, textAreaHeight)))
            {
                slot.TextBody = text;
                slot.Kind = string.IsNullOrWhiteSpace(text)
                    ? PanelSlotKind.Empty
                    : PanelSlotKind.TextCommand;
                EzConfig.Save();
            }

            PanelUiTextStyle.NotifyInputHover("##eqpOverlayTextBody");
        }
    }

    private static string ResolveOverlayKindLabel(PanelSlot slot) =>
        slot.Kind switch
        {
            PanelSlotKind.Empty or PanelSlotKind.TextCommand => T("slot.kind.textCommand"),
            PanelSlotKind.Action => InventorySlotHelper.IsItemSlotType(
                (RaptureHotbarModule.HotbarSlotType)slot.CommandType)
                ? T("slot.kind.item")
                : T("slot.kind.action"),
            PanelSlotKind.Macro => slot.MacroSet == 0 ? T("slot.kind.macroPersonal") : T("slot.kind.macroShared"),
            _ => KindLabel(slot.Kind),
        };

    private static void DrawOverlayMacroContent(PanelSlot slot)
    {
        ImGui.Spacing();

        if (slot.MacroSet != _previewMacroSet || slot.MacroIndex != _previewMacroIndex)
            RefreshMacroPreview(slot);

        if (!_previewAvailable)
        {
            ImGui.TextUnformatted(_previewStatusMessage);
            return;
        }

        using (ImRaii.Disabled())
        using (new PanelUiEditFieldStyleScope(C.PanelUi))
        using (PanelUiTextStyle.PushInputText(C.PanelUi, "##eqpOverlayMacroBody"))
        {
            var body = string.IsNullOrWhiteSpace(_previewMacroBodyText)
                ? T("slot.editor.macroNoLines")
                : _previewMacroBodyText;
            var textAreaHeight = Math.Max(80f, ImGui.GetContentRegionAvail().Y);
            ImGui.InputTextMultiline(
                "##eqpOverlayMacroBody",
                ref body,
                4096,
                new Vector2(-1f, textAreaHeight),
                ImGuiInputTextFlags.ReadOnly);
            PanelUiTextStyle.NotifyInputHover("##eqpOverlayMacroBody");
        }
    }

    internal static void ClearSlotContents(PanelSlot slot) => ClearSlot(slot);

    private static void ClearSlot(PanelSlot slot)
    {
        slot.Kind = PanelSlotKind.Empty;
        slot.Label = string.Empty;
        slot.IconId = 0;
        slot.TextBody = string.Empty;
        slot.CommandType = 0;
        slot.CommandId = 0;
        slot.MacroSet = 0;
        slot.MacroIndex = 0;
        EzConfig.Save();
    }

    private static bool IsTextCommandEditorSlot(PanelSlot slot) =>
        slot.Kind is PanelSlotKind.Empty or PanelSlotKind.TextCommand;

    private static string ResolveOverlayDisplayName(PanelSlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.Label))
            return slot.Label.Trim();

        var tooltip = SlotIconResolver.ResolveTooltip(slot);
        return string.IsNullOrWhiteSpace(tooltip) ? T("common.noName") : tooltip;
    }

    private static void DrawKindCombo(PanelSlot slot, string id = "##eqpKind")
    {
        using (new PanelUiEditFieldStyleScope(C.PanelUi))
        using (PanelUiTextStyle.PushText(C.PanelUi))
        {
            var kind = slot.Kind;
            if (ImGui.BeginCombo(id, KindLabel(kind)))
            {
                foreach (PanelSlotKind value in Enum.GetValues<PanelSlotKind>())
                {
                    if (ImGui.Selectable(KindLabel(value), value == kind))
                    {
                        slot.Kind = value;
                        EzConfig.Save();
                    }
                }

                ImGui.EndCombo();
            }
        }
    }

    private static void DrawLabelField(PanelSlot slot, string id)
    {
        var label = slot.Label;
        if (ImGui.InputTextWithHint(id, T("common.displayName"), ref label, 64))
        {
            slot.Label = label;
            EzConfig.Save();
        }
    }

    private static void DrawLabelAndIcon(PanelSlot slot)
    {
        DrawLabelField(slot, T("common.displayName"));

        var icon = (int)slot.IconId;
        if (ImGui.InputInt(T("slot.editor.iconId"), ref icon))
        {
            slot.IconId = (uint)Math.Max(0, icon);
            EzConfig.Save();
        }
    }

    private static void DrawActionFields(PanelSlot slot)
    {
        using (new PanelUiEditFieldStyleScope(C.PanelUi))
        using (PanelUiTextStyle.PushText(C.PanelUi))
        {
            var selected = CommonSlotTypes.FirstOrDefault(x => (byte)x.Type == slot.CommandType);
            if (ImGui.BeginCombo(T("slot.editor.commandType"), selected.Label))
            {
                foreach (var entry in CommonSlotTypes)
                {
                    if (ImGui.Selectable(entry.Label, entry.Type == selected.Type))
                    {
                        slot.CommandType = (byte)entry.Type;
                        EzConfig.Save();
                    }
                }

                ImGui.EndCombo();
            }
        }

        var commandId = (int)slot.CommandId;
        if (ImGui.InputInt(T("slot.editor.commandId"), ref commandId))
        {
            slot.CommandId = (uint)Math.Max(0, commandId);
            EzConfig.Save();
        }
    }

    private static void DrawMacroFields(PanelSlot slot)
    {
        var macroSet = slot.MacroSet;
        if (ImGui.RadioButton(T("slot.editor.macroPersonal"), macroSet == 0))
        {
            slot.MacroSet = 0;
            EzConfig.Save();
        }

        ImGui.SameLine();
        if (ImGui.RadioButton(T("slot.editor.macroShared"), macroSet == 1))
        {
            slot.MacroSet = 1;
            EzConfig.Save();
        }

        var macroIndex = slot.MacroIndex + 1;
        if (ImGui.SliderInt(T("slot.editor.macroNumber"), ref macroIndex, 1, 100))
        {
            slot.MacroIndex = (byte)(macroIndex - 1);
            EzConfig.Save();
        }

        DrawMacroContentPreview(slot);
    }

    private static void RefreshMacroPreview(PanelSlot slot)
    {
        _previewMacroSet = slot.MacroSet;
        _previewMacroIndex = slot.MacroIndex;
        var content = MacroContentReader.Read(slot);
        _previewAvailable = content.IsAvailable;
        _previewStatusMessage = content.StatusMessage;
        _previewDisplayText = content.DisplayText;
        _previewMacroBodyText = content.BodyText;
    }

    private static void DrawMacroContentPreview(PanelSlot slot)
    {
        ImGui.Spacing();
        MirageUi.Text(T("slot.editor.macroPreview"), MirageUi.Color.Secondary);

        var content = MacroContentReader.Read(slot);
        if (!content.IsAvailable)
        {
            MirageUi.Text(content.StatusMessage, MirageUi.Color.Warning);
            return;
        }

        var displayText = content.DisplayText;
        ImGui.InputTextMultiline(
            "##eqpMacroContentPreview",
            ref displayText,
            8192,
            new Vector2(-1f, 160f),
            ImGuiInputTextFlags.ReadOnly);

        MirageUi.Text(content.StatusMessage, MirageUi.Color.Secondary);
    }

    private static void DrawTextCommandFields(PanelSlot slot, bool overlayPanel)
    {
        var text = slot.TextBody;
        var editorHeight = overlayPanel ? 180f : 120f;
        if (ImGui.InputTextMultiline(T("slot.kind.textCommand"), ref text, 4096, new Vector2(-1f, editorHeight)))
        {
            slot.TextBody = text;
            EzConfig.Save();
        }

        if (!overlayPanel)
            MirageUi.Text(T("slot.editor.textCommandHint"), MirageUi.Color.Secondary);
    }

    private static string KindLabel(PanelSlotKind kind) =>
        kind switch
        {
            PanelSlotKind.Empty => T("slot.kind.empty"),
            PanelSlotKind.Action => T("slot.kind.action"),
            PanelSlotKind.Macro => T("slot.kind.macro"),
            PanelSlotKind.TextCommand => T("slot.kind.textCommand"),
            _ => kind.ToString(),
        };
}
