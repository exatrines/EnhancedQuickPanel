using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace EnhancedQuickPanel.Services;

/// <summary>Computes action charge/stack counts for a slot.</summary>
internal static unsafe class SlotChargeResolver
{
    public static (bool Show, int Count) Resolve(
        RaptureHotbarModule.HotbarSlot scratch,
        (RaptureHotbarModule.HotbarSlotType SlotType, uint ActionId) appearance,
        RaptureHotbarModule.HotbarSlotType commandType)
    {
        if (InventorySlotHelper.IsItemSlotType(commandType))
            return (false, 0);

        try
        {
            var charges = (int)scratch.GetApparentIconRecastCharges();
            if (appearance.SlotType == RaptureHotbarModule.HotbarSlotType.Action && appearance.ActionId != 0)
            {
                var maxCharges = ActionManager.GetMaxCharges(appearance.ActionId, 0);
                if (maxCharges <= 1)
                    return (false, 0);

                return (true, Math.Max(0, charges));
            }

            if (charges > 1)
                return (true, charges);

            return (false, 0);
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"[EQP] Action charge read failed ({appearance.SlotType} #{appearance.ActionId}): {ex.Message}");
            return (false, 0);
        }
    }
}

