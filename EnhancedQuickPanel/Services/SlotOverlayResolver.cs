using EnhancedQuickPanel.Models;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace EnhancedQuickPanel.Services;

/// <summary>Computes which overlay labels a slot should show and their values.</summary>
internal static unsafe class SlotOverlayResolver
{
    public static SlotOverlayInfo Resolve(PanelSlot slot, ResolvedSlotIcon icon, SlotRuntimeState runtime)
    {
        if (slot.Kind != PanelSlotKind.Action)
            return SlotOverlayInfo.None;

        var type = (RaptureHotbarModule.HotbarSlotType)slot.CommandType;
        if (type == RaptureHotbarModule.HotbarSlotType.Macro)
            return new SlotOverlayInfo(false, 0, false, true);

        return ResolveFromRuntime(type, runtime);
    }

    public static SlotOverlayInfo ResolveNative(
        int slotIndex,
        RaptureHotbarModule.HotbarSlotType type,
        uint commandId,
        ResolvedSlotIcon icon)
    {
        var overlay = ResolveFromRuntime(type, SlotRuntimeCache.Get(type, commandId, icon));

        if (!NativeQuickPanelUiReader.TryGetSlotOverlay(slotIndex, out var nativeOverlay))
            return overlay;

        var showMacro = overlay.ShowMacroIndicator || nativeOverlay.ShowMacroIndicator;
        var showQuantity = overlay.ShowQuantity || nativeOverlay.ShowQuantity;
        var quantity = nativeOverlay.ShowQuantity ? nativeOverlay.Quantity : overlay.Quantity;
        var showCharges = overlay.ShowActionCharges || nativeOverlay.ShowActionCharges;
        var charges = nativeOverlay.ShowActionCharges ? nativeOverlay.ActionCharges : overlay.ActionCharges;
        var isGrayedOut = overlay.IsGrayedOut || nativeOverlay.IsGrayedOut;

        if (!showQuantity && !showMacro && !isGrayedOut && !showCharges)
            return SlotOverlayInfo.None;

        return new SlotOverlayInfo(showQuantity, quantity, isGrayedOut, showMacro, showCharges, charges);
    }

    private static SlotOverlayInfo ResolveFromRuntime(
        RaptureHotbarModule.HotbarSlotType type,
        SlotRuntimeState runtime)
    {
        var showMacro = type == RaptureHotbarModule.HotbarSlotType.Macro;
        if (!InventorySlotHelper.IsItemSlotType(type))
        {
            var isGrayedOut = !runtime.IsUsable;
            if (showMacro)
                return new SlotOverlayInfo(false, 0, isGrayedOut, true, runtime.ShowActionCharges, runtime.ActionCharges);

            if (runtime.ShowActionCharges)
                return new SlotOverlayInfo(false, 0, isGrayedOut, false, true, runtime.ActionCharges);

            return runtime.IsUsable
                ? SlotOverlayInfo.None
                : new SlotOverlayInfo(false, 0, true, false);
        }

        var itemGrayedOut = runtime.ItemQuantity <= 0 || !runtime.IsUsable;
        return new SlotOverlayInfo(true, runtime.ItemQuantity, itemGrayedOut, showMacro);
    }

    internal static int ResolveItemQuantityScratch(
        RaptureHotbarModule.HotbarSlot scratch,
        RaptureHotbarModule* hotbar,
        RaptureHotbarModule.HotbarSlotType type,
        uint commandId,
        ResolvedSlotIcon icon,
        (RaptureHotbarModule.HotbarSlotType SlotType, uint ActionId) appearance)
    {
        try
        {
            scratch.LoadCostDataForSlot();

            var appearanceType = appearance.SlotType;
            var appearanceId = appearance.ActionId;
            if (appearanceType == RaptureHotbarModule.HotbarSlotType.Empty || appearanceId == 0)
            {
                appearanceType = type;
                appearanceId = commandId;
            }

            if (scratch.CostDisplayMode is 3 or 4)
                return (int)scratch.CostValue;

            var costValue = scratch.GetCostValueForSlot(appearanceType, appearanceId);
            if (InventorySlotHelper.IsItemSlotType(appearanceType))
                return (int)costValue;
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"[EQP] Item quantity read failed ({type} #{commandId}): {ex.Message}");
        }

        var isHq = icon.IsHighQuality || InventorySlotHelper.IsHighQuality(type, commandId);
        var (baseItemId, decodedHq) = InventorySlotHelper.DecodeItemId(commandId);
        return baseItemId == 0
            ? 0
            : InventorySlotHelper.GetInventoryItemCount(baseItemId, isHq || decodedHq);
    }
}

