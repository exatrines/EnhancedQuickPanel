using EnhancedQuickPanel.Models;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EnhancedQuickPanel.Services;

/// <summary>
/// MacroChain-compatible macro slot model:
/// GetMacro(set, index) where set 0 = individual, 1 = shared, index = 0..99.
/// Macro #N uses index N-1 (0-based). Hotbar commandId = set * 100 + index + 1.
/// QuickPanel: personal = index (0-99), shared = 256 + index (256-355).
/// </summary>
/// <summary>Encodes and decodes macro slot command IDs and resolves macro slots.</summary>
internal static unsafe class MacroSlotResolver
{
    private const uint SharedMacroCommandIdBase = 256;
    private const uint SharedMacroCommandIdMax = 355;

    public static uint EncodeHotbarCommandId(byte macroSet, byte macroIndex) =>
        (uint)(macroSet * 100 + macroIndex + 1);

    public static uint EncodeQuickPanelMacroCommandId(byte macroSet, byte macroIndex) =>
        macroSet == 0 ? macroIndex : SharedMacroCommandIdBase + macroIndex;

    public static uint ResolveExecutionCommandId(PanelSlot slot)
    {
        if (slot.Kind == PanelSlotKind.Macro)
            return EncodeQuickPanelMacroCommandId(slot.MacroSet, slot.MacroIndex);

        if ((RaptureHotbarModule.HotbarSlotType)slot.CommandType
            != RaptureHotbarModule.HotbarSlotType.Macro)
            return 0;

        if (TryDecodeQuickPanelMacroCommandId(slot.CommandId, out _, out _))
            return slot.CommandId;

        if (TryResolveMacroSlot(slot.CommandId, out var macroSet, out var macroIndex))
            return EncodeQuickPanelMacroCommandId(macroSet, macroIndex);

        if (slot.CommandId == 0)
            return 0;

        var (decodedSet, decodedIndex) = DecodeHotbarCommandId(slot.CommandId);
        return EncodeQuickPanelMacroCommandId(decodedSet, decodedIndex);
    }

    public static (byte MacroSet, byte MacroIndex) DecodeHotbarCommandId(uint commandId)
    {
        if (commandId == 0)
            return (0, 0);

        var macroSet = (byte)(commandId >= 101 ? 1 : 0);
        var slot = commandId % 100;
        var macroIndex = (byte)(slot == 0 ? 99 : slot - 1);
        return (macroSet, macroIndex);
    }

    public static (byte MacroSet, byte MacroIndex) DecodeQuickPanelMacroCommandId(uint commandId) =>
        TryDecodeQuickPanelMacroCommandId(commandId, out var macroSet, out var macroIndex)
            ? (macroSet, macroIndex)
            : ((byte)0, (byte)0);

    public static bool TryResolveMacroSlot(uint commandId, out byte macroSet, out byte macroIndex)
    {
        macroSet = 0;
        macroIndex = 0;

        if (commandId is >= SharedMacroCommandIdBase and <= SharedMacroCommandIdMax)
        {
            macroSet = 1;
            macroIndex = (byte)(commandId - SharedMacroCommandIdBase);
            return true;
        }

        if (commandId <= 99)
        {
            macroIndex = (byte)commandId;
            return true;
        }

        if (commandId is >= 101 and <= 200)
        {
            (macroSet, macroIndex) = DecodeHotbarCommandId(commandId);
            return true;
        }

        if (commandId == 100)
        {
            macroIndex = 99;
            return true;
        }

        return false;
    }

    public static void ApplyMacroSlot(PanelSlot slot, byte macroSet, byte macroIndex)
    {
        slot.Kind = PanelSlotKind.Macro;
        slot.MacroSet = macroSet;
        slot.MacroIndex = macroIndex;
        slot.CommandId = EncodeQuickPanelMacroCommandId(macroSet, macroIndex);
        slot.CommandType = 0;
    }

    public static bool TryResolveMacroDragPayload(
        int int1,
        int int2,
        short referenceIndex,
        string macroName,
        out byte macroSet,
        out byte macroIndex)
    {
        macroSet = 0;
        macroIndex = 0;

        if (!TryResolveMacroDragIndex(int1, int2, referenceIndex, out macroIndex))
            return false;

        macroSet = ResolveMacroDragSet(int1, int2, macroIndex, macroName);
        return true;
    }

    private static byte ResolveMacroDragSet(
        int int1,
        int int2,
        byte macroIndex,
        string macroName)
    {
        if (TryGetAgentMacroSelectedSet(out var agentSet))
            return agentSet;

        if (TryGetMacroAddonSelectedPage(out var addonSet))
            return addonSet;

        if (TryMatchMacroSetByDraggedName(macroName, macroIndex, out var nameSet))
            return nameSet;

        if (TryDisambiguateMacroSetByContent(macroIndex, out var contentSet))
            return contentSet;

        // Int2 is occasionally the set when UI state is unavailable.
        if (int2 is 0 or 1)
            return (byte)int2;

        return 0;
    }

    private static bool TryGetAgentMacroSelectedSet(out byte macroSet)
    {
        macroSet = 0;

        var agentModule = AgentModule.Instance();
        if (agentModule == null)
            return false;

        var agent = (AgentMacro*)agentModule->GetAgentByInternalId(AgentId.Macro);
        if (agent == null)
            return false;

        if (agent->SelectedMacroSet > 1)
            return false;

        macroSet = (byte)agent->SelectedMacroSet;
        return true;
    }

    private static bool TryGetMacroAddonSelectedPage(out byte macroSet)
    {
        macroSet = 0;

        if (!GenericHelpers.TryGetAddonByName<AddonMacro>("Macro", out var macroAddon)
            || !GenericHelpers.IsAddonReady((AtkUnitBase*)macroAddon))
            return false;

        if (macroAddon->SelectedPage > 1)
            return false;

        macroSet = (byte)macroAddon->SelectedPage;
        return true;
    }

    private static bool TryMatchMacroSetByDraggedName(
        string draggedName,
        byte macroIndex,
        out byte macroSet)
    {
        macroSet = 0;

        if (string.IsNullOrWhiteSpace(draggedName)
            || !GameModuleGuard.TryGetMacroModule(out var macroModule))
            return false;

        try
        {
            var personalMacro = macroModule->GetMacro(0, macroIndex);
            var sharedMacro = macroModule->GetMacro(1, macroIndex);
            if (personalMacro == null || sharedMacro == null)
                return false;

            var personalName = SeStringTextHelper.ReadPlainText(personalMacro->Name);
            var sharedName = SeStringTextHelper.ReadPlainText(sharedMacro->Name);

            var personalMatch = NamesMatch(draggedName, personalName);
            var sharedMatch = NamesMatch(draggedName, sharedName);

            if (personalMatch && !sharedMatch)
            {
                macroSet = 0;
                return true;
            }

            if (sharedMatch && !personalMatch)
            {
                macroSet = 1;
                return true;
            }
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"[EQP] Macro name match failed (index={macroIndex}): {ex.Message}");
        }

        return false;
    }

    private static bool TryDisambiguateMacroSetByContent(byte macroIndex, out byte macroSet)
    {
        macroSet = 0;

        if (!GameModuleGuard.TryGetMacroModule(out var macroModule))
            return false;

        try
        {
            var personalMacro = macroModule->GetMacro(0, macroIndex);
            var sharedMacro = macroModule->GetMacro(1, macroIndex);
            if (personalMacro == null || sharedMacro == null)
                return false;

            var personalExists = personalMacro->IsNotEmpty();
            var sharedExists = sharedMacro->IsNotEmpty();

            if (personalExists && !sharedExists)
            {
                macroSet = 0;
                return true;
            }

            if (sharedExists && !personalExists)
            {
                macroSet = 1;
                return true;
            }
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"[EQP] Macro content disambiguation failed (index={macroIndex}): {ex.Message}");
        }

        return false;
    }

    private static bool NamesMatch(string left, string right) =>
        !string.IsNullOrWhiteSpace(left)
        && !string.IsNullOrWhiteSpace(right)
        && string.Equals(left.Trim(), right.Trim(), StringComparison.Ordinal);

    private static bool TryResolveMacroDragIndex(
        int int1,
        int int2,
        short referenceIndex,
        out byte macroIndex)
    {
        macroIndex = 0;

        if (referenceIndex is >= 0 and <= 99)
        {
            macroIndex = (byte)referenceIndex;
            return true;
        }

        if (TryGetAgentMacroSelectedIndex(out macroIndex))
            return true;

        if (int1 is >= 0 and <= 99)
        {
            macroIndex = (byte)int1;
            return true;
        }

        if (int2 is >= 2 and <= 99)
        {
            macroIndex = (byte)int2;
            return true;
        }

        return false;
    }

    private static bool TryGetAgentMacroSelectedIndex(out byte macroIndex)
    {
        macroIndex = 0;

        var agentModule = AgentModule.Instance();
        if (agentModule == null)
            return false;

        var agent = (AgentMacro*)agentModule->GetAgentByInternalId(AgentId.Macro);
        if (agent == null || agent->SelectedMacroIndex > 99)
            return false;

        macroIndex = (byte)agent->SelectedMacroIndex;
        return true;
    }

    public static bool TryResolveImportMacro(
        RaptureHotbarModule.HotbarSlotType type,
        uint commandId,
        out byte macroSet,
        out byte macroIndex) =>
        TryDecodeQuickPanelMacroCommandId(commandId, out macroSet, out macroIndex)
        && type == RaptureHotbarModule.HotbarSlotType.Macro;

    public static bool TryDecodeQuickPanelMacroCommandId(
        uint commandId,
        out byte macroSet,
        out byte macroIndex)
    {
        macroSet = 0;
        macroIndex = 0;

        if (commandId <= 99)
        {
            macroIndex = (byte)commandId;
            return true;
        }

        if (commandId is >= SharedMacroCommandIdBase and <= SharedMacroCommandIdMax)
        {
            macroSet = 1;
            macroIndex = (byte)(commandId - SharedMacroCommandIdBase);
            return true;
        }

        return false;
    }

    public static bool TryResolveScratchHotbarCommandId(uint commandId, out uint hotbarCommandId)
    {
        hotbarCommandId = 0;
        if (commandId == 0)
            return false;

        return ScratchSlotHelper.TryResolveCommand(
            RaptureHotbarModule.HotbarSlotType.Macro,
            commandId,
            out var resolvedType,
            out hotbarCommandId)
            && resolvedType == RaptureHotbarModule.HotbarSlotType.Macro;
    }
}

