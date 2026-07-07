using EnhancedQuickPanel.Models;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace EnhancedQuickPanel.Services;

/// <summary>Determines whether a slot's action is currently usable (grayed out when not).</summary>
internal static unsafe class SlotAvailabilityResolver
{
    public static bool IsUsable(PanelSlot slot)
    {
        if (slot.Kind != PanelSlotKind.Action || slot.CommandId == 0)
            return true;

        var type = (RaptureHotbarModule.HotbarSlotType)slot.CommandType;
        if (type is RaptureHotbarModule.HotbarSlotType.Empty
            or RaptureHotbarModule.HotbarSlotType.Macro)
        {
            return true;
        }

        return SlotRuntimeCache.Get(slot, ResolvedSlotIcon.Empty).IsUsable;
    }

    internal static (RaptureHotbarModule.HotbarSlotType SlotType, uint ActionId) ResolveAppearance(
        RaptureHotbarModule.HotbarSlot scratch,
        RaptureHotbarModule* hotbar)
    {
        RaptureHotbarModule.HotbarSlotType appearanceType = default;
        uint appearanceId = 0;
        ushort appearanceData = 0;
        RaptureHotbarModule.GetSlotAppearance(
            &appearanceType,
            &appearanceId,
            &appearanceData,
            hotbar,
            &scratch);

        var slotType = appearanceType != RaptureHotbarModule.HotbarSlotType.Empty
            ? appearanceType
            : scratch.ApparentSlotType != RaptureHotbarModule.HotbarSlotType.Empty
                ? scratch.ApparentSlotType
                : scratch.CommandType;
        var actionId = appearanceId != 0
            ? appearanceId
            : scratch.ApparentActionId != 0
                ? scratch.ApparentActionId
                : scratch.CommandId;

        return (slotType, actionId);
    }
}

