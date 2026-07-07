using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EnhancedQuickPanel.Services;

/// <summary>Resolves drag sources from buddy (chocobo) inventory.</summary>
internal static unsafe class InventoryBuddyDragSourceResolver
{
    internal const int SlotsPerBag = 35;
    internal const int UiSlotCount = 70;

    private static readonly string[] BuddyAddonNames =
    [
        "InventoryBuddy",
        "InventoryBuddy2",
    ];

    public static bool TryBuildDragHint(
        AtkDragDropInterface* dragInterface,
        out InventoryDragSourceHint hint)
    {
        hint = InventoryDragSourceHint.Empty;

        if (!TryFindBuddyLocation(dragInterface, out var addonName, out var uiSlotIndex, out var component)
            || !TryMapBuddyUiSlot(addonName, uiSlotIndex, out var container, out var inventorySlot))
            return false;

        hint = new InventoryDragSourceHint
        {
            PreferredContainer = container,
            LabelUiGridSlot = inventorySlot,
            LabelSourceName = $"{addonName}#{uiSlotIndex + 1}",
            SlotComponent = component,
        };
        return true;
    }

    public static bool TryFindBuddyLocation(
        AtkDragDropInterface* dragInterface,
        out string addonName,
        out int uiSlotIndex,
        out AtkComponentDragDrop* slotComponent)
    {
        addonName = string.Empty;
        uiSlotIndex = -1;
        slotComponent = null;

        if (dragInterface == null)
            return false;

        foreach (var candidateAddonName in BuddyAddonNames)
        {
            if (!TryFindSlotInBuddyAddon(candidateAddonName, dragInterface, out uiSlotIndex, out slotComponent))
                continue;

            addonName = candidateAddonName;
            return true;
        }

        return false;
    }

    public static bool TryResolveFromActiveDragInterface(
        AtkDragDropInterface* dragInterface,
        short referenceIndex,
        out InventoryType container,
        out int slotIndex,
        out string sourceLabel)
    {
        container = default;
        slotIndex = -1;
        sourceLabel = string.Empty;

        if (!TryFindBuddyLocation(dragInterface, out var addonName, out var uiSlotIndex, out var component)
            || !TryMapBuddyUiSlot(addonName, uiSlotIndex, out container, out var inventorySlot))
            return false;

        return InventoryRemoteDragResolver.TryResolveFromLocatedSlot(
            component,
            referenceIndex,
            container,
            inventorySlot,
            $"{addonName}#{uiSlotIndex + 1}",
            out container,
            out slotIndex,
            out sourceLabel);
    }

    public static bool TryMapBuddyUiSlot(
        string addonName,
        int uiSlotIndex,
        out InventoryType container,
        out int inventorySlot)
    {
        container = default;
        inventorySlot = -1;

        if (uiSlotIndex < 0 || uiSlotIndex >= UiSlotCount)
            return false;

        inventorySlot = uiSlotIndex % SlotsPerBag;
        if (!InventorySlotHelper.IsPlausibleSlotIndexPublic(inventorySlot))
            return false;

        container = addonName switch
        {
            "InventoryBuddy" when uiSlotIndex < SlotsPerBag => InventoryType.SaddleBag1,
            "InventoryBuddy" => InventoryType.SaddleBag2,
            "InventoryBuddy2" when uiSlotIndex < SlotsPerBag => InventoryType.PremiumSaddleBag1,
            "InventoryBuddy2" => InventoryType.PremiumSaddleBag2,
            _ => InventoryType.Invalid,
        };

        return container != InventoryType.Invalid;
    }

    private static bool TryFindSlotInBuddyAddon(
        string addonName,
        AtkDragDropInterface* dragInterface,
        out int uiSlotIndex,
        out AtkComponentDragDrop* slotComponent)
    {
        uiSlotIndex = -1;
        slotComponent = null;

        if (!GenericHelpers.TryGetAddonByName<AddonInventoryBuddy>(addonName, out var buddy)
            || !GenericHelpers.IsAddonReady((AtkUnitBase*)buddy))
            return false;

        var slots = buddy->Slots;
        for (var i = 0; i < slots.Length; i++)
        {
            var component = slots[i].Value;
            if (component == null)
                continue;

            if (&component->AtkDragDropInterface != dragInterface)
                continue;

            uiSlotIndex = i;
            slotComponent = component;
            return true;
        }

        return false;
    }
}

