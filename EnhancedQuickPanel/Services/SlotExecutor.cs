using EnhancedQuickPanel.Models;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace EnhancedQuickPanel.Services;

/// <summary>Executes a slot's configured action, macro, or text command.</summary>
internal static class SlotExecutor
{
    public static void ExecuteNative(RaptureHotbarModule.HotbarSlotType type, uint commandId)
    {
        if (type == RaptureHotbarModule.HotbarSlotType.Empty)
            return;

        if (type != RaptureHotbarModule.HotbarSlotType.Macro && commandId == 0)
            return;

        HotbarScratchSlot.TryExecute(type, commandId);
    }

    public static void Execute(PanelSlot slot)
    {
        if (!slot.IsConfigured || !GameModuleGuard.IsClientReady)
            return;

        switch (slot.Kind)
        {
            case PanelSlotKind.Plugin:
                PluginShortcuts.Execute(slot, ImGuiMouseButton.Left);
                break;
            case PanelSlotKind.Action:
                if ((RaptureHotbarModule.HotbarSlotType)slot.CommandType
                    == RaptureHotbarModule.HotbarSlotType.Macro)
                    ExecuteMacroCommandId(MacroSlotResolver.ResolveExecutionCommandId(slot));
                else
                    ExecuteAction(slot);
                break;
            case PanelSlotKind.Macro:
                ExecuteMacro(slot);
                break;
            case PanelSlotKind.TextCommand:
                ExecuteTextCommand(slot);
                break;
            case PanelSlotKind.Dalamud:
                DalamudShortcuts.Execute(slot);
                break;
        }
    }

    private static void ExecuteAction(PanelSlot slot) =>
        ExecuteNative((RaptureHotbarModule.HotbarSlotType)slot.CommandType, slot.CommandId);

    private static void ExecuteMacro(PanelSlot slot) =>
        ExecuteMacroCommandId(MacroSlotResolver.ResolveExecutionCommandId(slot));

    private static void ExecuteMacroCommandId(uint commandId) =>
        ExecuteNative(RaptureHotbarModule.HotbarSlotType.Macro, commandId);

    private static void ExecuteTextCommand(PanelSlot slot)
    {
        if (string.IsNullOrWhiteSpace(slot.TextBody))
            return;

        TextCommandExecutor.Execute(slot.TextBody);
    }
}
