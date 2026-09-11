using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace EnhancedQuickPanel.Services;

/// <summary>Verifies inventory drag results and restores scratch-slot state.</summary>
internal static unsafe class InventoryDragSafety
{
    public static bool TryVerifyDragResolution(
        ResolvedSlotIcon appearance,
        InventoryType container,
        int slotIndex)
    {
        if (!InventorySlotHelper.IsDragResolvableInventoryType((int)container)
            || !InventorySlotHelper.IsPlausibleSlotIndexForContainer(container, slotIndex)
            || !GameModuleGuard.TryGetInventory(out var inventory))
            return false;

        try
        {
            var inventoryItem = InventorySlotHelper.ResolveSymbolicInventoryItem(
                inventory->GetInventorySlot(container, slotIndex));
            if (inventoryItem == null || inventoryItem->IsEmpty())
                return false;

            if (appearance.IconId == 0)
                return true;

            var resolved = SlotIconResolver.ResolveInventoryItemAppearance(inventoryItem);
            if (resolved.IconId == appearance.IconId)
                return !appearance.IsHighQuality || inventoryItem->IsHighQuality();

            return TryVerifyDragResolutionByGameIcon(appearance, container, slotIndex, inventoryItem);
        }
        catch (Exception ex)
        {
            PluginServices.Log.Debug($"[EQP] Drag resolution verify failed ({container}#{slotIndex + 1}): {ex.Message}");
            return false;
        }
    }

    private static bool TryVerifyDragResolutionByGameIcon(
        ResolvedSlotIcon appearance,
        InventoryType container,
        int slotIndex,
        InventoryItem* inventoryItem)
    {
        if (!GameModuleGuard.TryGetHotbar(out var hotbar, out var uiModule))
            return false;

        try
        {
            var encoded = InventorySlotHelper.EncodeInventoryCommand(container, slotIndex);
            var scratch = hotbar->ScratchSlot;
            scratch.Set(uiModule, RaptureHotbarModule.HotbarSlotType.InventoryItem, encoded);
            scratch.LoadIconId();

            RaptureHotbarModule.HotbarSlotType appearanceType = default;
            uint appearanceId = 0;
            ushort appearanceData = 0;
            RaptureHotbarModule.GetSlotAppearance(
                &appearanceType,
                &appearanceId,
                &appearanceData,
                hotbar,
                &scratch);

            if (appearanceType == RaptureHotbarModule.HotbarSlotType.Empty || appearanceId == 0)
            {
                appearanceType = RaptureHotbarModule.HotbarSlotType.InventoryItem;
                appearanceId = encoded;
            }

            var gameIcon = scratch.GetIconIdForSlot(appearanceType, appearanceId);
            if (gameIcon <= 0 || (uint)gameIcon != appearance.IconId)
                return false;

            return !appearance.IsHighQuality || inventoryItem->IsHighQuality();
        }
        catch
        {
            return false;
        }
    }

    public static bool TryWithScratchRestore(Func<bool> action)
    {
        if (!GameModuleGuard.TryGetHotbar(out var hotbar, out var uiModule))
            return false;

        RaptureHotbarModule.HotbarSlotType savedType = default;
        uint savedCommandId = 0;
        var shouldRestore = false;
        var result = false;

        try
        {
            var scratch = hotbar->ScratchSlot;
            savedType = scratch.CommandType;
            savedCommandId = scratch.CommandId;
            shouldRestore = savedType != RaptureHotbarModule.HotbarSlotType.Empty && savedCommandId != 0;

            result = action();
        }
        catch (Exception ex)
        {
            PluginServices.Log.Debug($"[EQP] Scratch-restore action failed: {ex.Message}");
            result = false;
        }

        if (shouldRestore)
        {
            try
            {
                hotbar->ScratchSlot.Set(uiModule, savedType, savedCommandId);
            }
            catch (Exception ex)
            {
                PluginServices.Log.Debug($"[EQP] Scratch slot restore failed: {ex.Message}");
            }
        }

        return result;
    }

    public static bool HasInventoryItemAt(InventoryType container, int slotIndex) =>
        InventorySlotHelper.TryInventorySlotHasItem(container, slotIndex, expectedItemId: null);
}

