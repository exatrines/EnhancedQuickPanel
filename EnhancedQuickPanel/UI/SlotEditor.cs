using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using EnhancedQuickPanel.Models;
using EnhancedQuickPanel.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace EnhancedQuickPanel.UI;

// Slot content editor shown beside the panel grid.
internal static class SlotEditor
{
    private static byte _previewMacroSet = byte.MaxValue;
    private static byte _previewMacroIndex = byte.MaxValue;
    private static bool _previewAvailable;
    private static string _previewStatusMessage = string.Empty;
    private static string _previewMacroBodyText = string.Empty;

    private static readonly PanelSlotKind[] EditableKinds =
    [
        PanelSlotKind.TextCommand,
        PanelSlotKind.Dalamud,
        PanelSlotKind.Plugin,
    ];

    public static void Draw(PanelSlot slot, ref bool slotEditorExpanded)
    {
        DrawHeaderBar(slot, ref slotEditorExpanded);
        if (slot.Kind == PanelSlotKind.Plugin)
        {
            using (new PanelUiEditFieldStyleScope(Config.PanelUi))
            using (PanelUiTextStyle.PushText(Config.PanelUi))
                PluginShortcutEditor.DrawUnavailableWarning(slot);

            using (new PanelUiEditFieldStyleScope(Config.PanelUi))
                PluginShortcutPicker.DrawEmbedded(slot);
        }
        else if (IsTextCommandEditorSlot(slot))
            DrawTextArea(slot);
        else if (slot.Kind == PanelSlotKind.Macro)
            DrawMacroContent(slot);
    }

    private static void DrawHeaderBar(PanelSlot slot, ref bool slotEditorExpanded)
    {
        var style = Config.PanelUi;
        var isPlugin = slot.Kind == PanelSlotKind.Plugin;
        var isDalamud = slot.Kind == PanelSlotKind.Dalamud;
        var iconInteractive = IsTextCommandEditorSlot(slot) || isPlugin || isDalamud;
        var trailingButtonCount = isPlugin ? 1 : 2;
        var blockHeight = ImGui.GetFrameHeight() * 2f + ImGui.GetStyle().ItemSpacing.Y;
        var iconButtonSize = new Vector2(blockHeight, blockHeight);
        var actionButtonSize = new Vector2(ImGui.GetFrameHeight(), ImGui.GetFrameHeight());
        var spacing = ImGui.GetStyle().ItemSpacing.X;

        using (new PanelUiEditFieldStyleScope(style))
        {
            SlotIconPicker.DrawIconButton(slot, iconButtonSize, interactive: iconInteractive);
            ImGui.SameLine(0f, spacing);

            var contentWidth = ImGui.GetContentRegionAvail().X;
            var nameRowWidth = Math.Max(
                32f,
                contentWidth - actionButtonSize.X * trailingButtonCount - spacing * trailingButtonCount);

            ImGui.BeginGroup();

            var kindRowStartX = ImGui.GetCursorPosX();
            if (CanSwitchKind(slot))
            {
                ImGui.PushItemWidth(Math.Max(32f, contentWidth - actionButtonSize.X - spacing));
                DrawKindCombo(slot);
                ImGui.PopItemWidth();
            }
            else
                ImGui.TextUnformatted(ResolveKindLabel(slot));
            ImGui.SameLine();
            ImGui.SetCursorPosX(kindRowStartX + contentWidth - actionButtonSize.X);
            using (new PanelUiButtonStyleScope(style))
            {
                var expandIcon = slotEditorExpanded
                    ? FontAwesomeIcon.AngleDoubleLeft
                    : FontAwesomeIcon.AngleDoubleRight;
                if (CenteredIconButton.Draw(
                        expandIcon,
                        "##eqpSlotEditorExpand",
                        actionButtonSize,
                        style.TextColor,
                        style.TextHoverColor))
                    slotEditorExpanded = !slotEditorExpanded;
            }

            if (isPlugin)
                PluginShortcutEditor.DrawCurrentName(slot, nameRowWidth);
            else if (isDalamud)
                DalamudShortcuts.DrawCombo(slot, nameRowWidth, "##eqpSlotEditorDalamud");
            else if (IsTextCommandEditorSlot(slot))
            {
                var label = slot.Label;
                ImGui.PushItemWidth(nameRowWidth);
                using (PanelUiTextStyle.PushInputText(style, "##eqpSlotEditorName"))
                {
                    if (ImGui.InputTextWithHint("##eqpSlotEditorName", T("common.name"), ref label, 64))
                    {
                        slot.Label = label;
                        Config.Save();
                    }

                    PanelUiTextStyle.NotifyInputHover("##eqpSlotEditorName");
                }

                ImGui.PopItemWidth();
            }
            else
            {
                ImGui.PushItemWidth(nameRowWidth);
                using (ImRaii.Disabled())
                using (PanelUiTextStyle.PushTextDisabled(style))
                {
                    var displayName = ResolveDisplayName(slot);
                    ImGui.InputTextWithHint("##eqpSlotEditorName", T("common.name"), ref displayName, 64, ImGuiInputTextFlags.ReadOnly);
                }

                ImGui.PopItemWidth();
            }

            ImGui.SameLine(0f, spacing);
            using (new PanelUiButtonStyleScope(style))
            {
                if (!isPlugin)
                {
                    var canExecute = slot.IsConfigured;
                    if (CenteredIconButton.Draw(
                            FontAwesomeIcon.Play,
                            "##eqpSlotEditorExecute",
                            actionButtonSize,
                            style.TextColor,
                            style.TextHoverColor,
                            enabled: canExecute)
                        && canExecute)
                        SlotExecutor.Execute(slot);

                    ImGui.SameLine(0f, spacing);
                }

                var ctrlHeld = ImGui.GetIO().KeyCtrl;
                if (CenteredIconButton.Draw(
                        FontAwesomeIcon.Trash,
                        "##eqpSlotEditorClear",
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

    private static bool CanSwitchKind(PanelSlot slot) =>
        slot.Kind is PanelSlotKind.Empty || EditableKinds.Contains(slot.Kind);

    private static void DrawKindCombo(PanelSlot slot)
    {
        var current = EditableKinds.Contains(slot.Kind) ? slot.Kind : PanelSlotKind.TextCommand;
        if (!ImGui.BeginCombo("##eqpSlotEditorKind", KindLabel(current)))
            return;

        foreach (var kind in EditableKinds)
        {
            if (ImGui.Selectable(KindLabel(kind), current == kind) && current != kind)
                ApplyKind(slot, kind);
        }

        ImGui.EndCombo();
    }

    private static void ApplyKind(PanelSlot slot, PanelSlotKind kind)
    {
        slot.Label = string.Empty;
        slot.CommandType = 0;
        slot.CommandId = 0;
        slot.MacroSet = 0;
        slot.MacroIndex = 0;
        slot.ResetPluginShortcut();
        slot.ResetDalamudShortcut();
        if (kind == PanelSlotKind.Plugin)
        {
            slot.Kind = PanelSlotKind.Plugin;
            slot.TextBody = string.Empty;
            slot.IconId = 0;
        }
        else if (kind == PanelSlotKind.Dalamud)
        {
            slot.Kind = PanelSlotKind.Dalamud;
            slot.TextBody = string.Empty;
            slot.IconId = 0;
        }
        else
        {
            slot.Kind = string.IsNullOrWhiteSpace(slot.TextBody)
                ? PanelSlotKind.Empty
                : PanelSlotKind.TextCommand;
        }

        Config.Save();
    }

    private static void DrawTextArea(PanelSlot slot)
    {
        ImGui.Spacing();

        var style = Config.PanelUi;
        var text = slot.TextBody;
        var textAreaHeight = Math.Max(80f, ImGui.GetContentRegionAvail().Y);
        using (new PanelUiEditFieldStyleScope(style))
        using (PanelUiTextStyle.PushInputText(style, "##eqpSlotEditorTextBody"))
        {
            if (ImGui.InputTextMultiline("##eqpSlotEditorTextBody", ref text, 4096, new Vector2(-1f, textAreaHeight)))
            {
                slot.TextBody = text;
                slot.Kind = string.IsNullOrWhiteSpace(text)
                    ? PanelSlotKind.Empty
                    : PanelSlotKind.TextCommand;
                Config.Save();
            }

            PanelUiTextStyle.NotifyInputHover("##eqpSlotEditorTextBody");
        }
    }

    private static string ResolveKindLabel(PanelSlot slot) =>
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

    private static void DrawMacroContent(PanelSlot slot)
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
        using (new PanelUiEditFieldStyleScope(Config.PanelUi))
        using (PanelUiTextStyle.PushInputText(Config.PanelUi, "##eqpSlotEditorMacroBody"))
        {
            var body = string.IsNullOrWhiteSpace(_previewMacroBodyText)
                ? T("slot.editor.macroNoLines")
                : _previewMacroBodyText;
            var textAreaHeight = Math.Max(80f, ImGui.GetContentRegionAvail().Y);
            ImGui.InputTextMultiline(
                "##eqpSlotEditorMacroBody",
                ref body,
                4096,
                new Vector2(-1f, textAreaHeight),
                ImGuiInputTextFlags.ReadOnly);
            PanelUiTextStyle.NotifyInputHover("##eqpSlotEditorMacroBody");
        }
    }

    internal static void ClearSlotContents(PanelSlot slot)
    {
        slot.Clear();
        Config.Save();
    }

    private static bool IsTextCommandEditorSlot(PanelSlot slot) =>
        slot.Kind is PanelSlotKind.Empty or PanelSlotKind.TextCommand;

    private static string ResolveDisplayName(PanelSlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.Label))
            return slot.Label.Trim();

        var tooltip = SlotIconResolver.ResolveTooltip(slot);
        return string.IsNullOrWhiteSpace(tooltip) ? T("common.noName") : tooltip;
    }

    private static void RefreshMacroPreview(PanelSlot slot)
    {
        _previewMacroSet = slot.MacroSet;
        _previewMacroIndex = slot.MacroIndex;
        SlotIconResolver.InvalidateMacro(slot.MacroSet, slot.MacroIndex);
        var content = MacroContentReader.Read(slot);
        _previewAvailable = content.IsAvailable;
        _previewStatusMessage = content.StatusMessage;
        _previewMacroBodyText = content.BodyText;
    }

    private static string KindLabel(PanelSlotKind kind) =>
        kind switch
        {
            PanelSlotKind.Empty => T("slot.kind.empty"),
            PanelSlotKind.Action => T("slot.kind.action"),
            PanelSlotKind.Macro => T("slot.kind.macro"),
            PanelSlotKind.TextCommand => T("slot.kind.textCommand"),
            PanelSlotKind.Dalamud => T("slot.kind.dalamud"),
            PanelSlotKind.Plugin => T("slot.kind.plugin"),
            _ => kind.ToString(),
        };
}
