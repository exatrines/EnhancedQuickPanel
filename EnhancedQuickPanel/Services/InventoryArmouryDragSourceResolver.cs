using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EnhancedQuickPanel.Services;

/// <summary>Resolves drag sources from the armoury chest.</summary>
internal static unsafe class InventoryArmouryDragSourceResolver
{
    internal const string AddonName = "ArmouryBoard";
    internal const int UiSlotCount = 50;

    private static readonly InventoryType[] TabIndexToContainer =
    [
        InventoryType.ArmoryMainHand,
        InventoryType.ArmoryOffHand,
        InventoryType.ArmoryHead,
        InventoryType.ArmoryBody,
        InventoryType.ArmoryHands,
        InventoryType.ArmoryLegs,
        InventoryType.ArmoryFeets,
        InventoryType.ArmoryEar,
        InventoryType.ArmoryNeck,
        InventoryType.ArmoryWrist,
        InventoryType.ArmoryRings,
        InventoryType.ArmorySoulCrystal,
    ];

    public static bool TryBuildDragHint(
        AtkDragDropInterface* dragInterface,
        out InventoryDragSourceHint hint)
    {
        hint = InventoryDragSourceHint.Empty;

        if (!TryFindArmouryLocation(dragInterface, out var tabIndex, out var uiSlotIndex, out var component)
            || !TryMapArmouryUiSlot(tabIndex, uiSlotIndex, out var container, out var inventorySlot))
            return false;

        hint = new InventoryDragSourceHint
        {
            PreferredContainer = container,
            LabelUiGridSlot = inventorySlot,
            LabelSourceName = $"{AddonName}#{uiSlotIndex + 1}",
            SlotComponent = component,
        };
        return true;
    }

    public static bool TryFindArmouryLocation(
        AtkDragDropInterface* dragInterface,
        out int tabIndex,
        out int uiSlotIndex,
        out AtkComponentDragDrop* slotComponent)
    {
        tabIndex = -1;
        uiSlotIndex = -1;
        slotComponent = null;

        if (dragInterface == null)
            return false;

        if (!GenericHelpers.TryGetAddonByName<AddonArmouryBoard>(AddonName, out var armoury)
            || !GenericHelpers.IsAddonReady((AtkUnitBase*)armoury))
            return false;

        tabIndex = armoury->TabIndex;
        var slots = armoury->Slots;
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

        if (!TryFindArmouryLocation(dragInterface, out var tabIndex, out var uiSlotIndex, out var component)
            || !TryMapArmouryUiSlot(tabIndex, uiSlotIndex, out container, out var inventorySlot))
            return false;

        return InventoryRemoteDragResolver.TryResolveFromLocatedSlot(
            component,
            referenceIndex,
            container,
            inventorySlot,
            $"{AddonName}#{uiSlotIndex + 1}",
            out container,
            out slotIndex,
            out sourceLabel);
    }

    public static bool TryMapArmouryUiSlot(
        int tabIndex,
        int uiSlotIndex,
        out InventoryType container,
        out int inventorySlot)
    {
        container = default;
        inventorySlot = -1;

        if (tabIndex < 0 || tabIndex >= TabIndexToContainer.Length)
            return false;

        if (uiSlotIndex < 0 || uiSlotIndex >= UiSlotCount)
            return false;

        container = TabIndexToContainer[tabIndex];
        inventorySlot = uiSlotIndex;

        return InventorySlotHelper.IsPlausibleSlotIndexForContainer(container, inventorySlot);
    }
}

