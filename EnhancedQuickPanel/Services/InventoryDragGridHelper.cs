using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EnhancedQuickPanel.Services;

/// <summary>Helpers for mapping inventory grid coordinates during drags.</summary>
internal static unsafe class InventoryDragGridHelper
{
    private static readonly string[] MainGridAddonNames =
    [
        "InventoryGrid0",
        "InventoryGrid1",
        "InventoryGrid2",
        "InventoryGrid3",
        "InventoryGrid0E",
        "InventoryGrid1E",
        "InventoryGrid2E",
        "InventoryGrid3E",
    ];

    public static bool TryFindSlotInGridAddon(
        string gridAddonName,
        AtkComponentDragDrop* source,
        AtkDragDropInterface* dragInterface,
        out int slotIndex,
        out AtkComponentDragDrop* slotComponent)
    {
        slotIndex = -1;
        slotComponent = null;

        if (!GenericHelpers.TryGetAddonByName<AddonInventoryGrid>(gridAddonName, out var grid)
            || !GenericHelpers.IsAddonReady((AtkUnitBase*)grid))
            return false;

        var slots = grid->Slots;

        if (dragInterface != null
            && dragInterface->DragDropReferenceIndex >= 0
            && dragInterface->DragDropReferenceIndex < slots.Length)
        {
            var referenced = slots[dragInterface->DragDropReferenceIndex].Value;
            if (referenced != null
                && &referenced->AtkDragDropInterface == dragInterface)
            {
                slotIndex = dragInterface->DragDropReferenceIndex;
                slotComponent = referenced;
                return true;
            }
        }

        for (var i = 0; i < slots.Length; i++)
        {
            var candidate = slots[i].Value;
            if (candidate == null)
                continue;

            if (source != null && candidate == source)
            {
                slotIndex = i;
                slotComponent = candidate;
                return true;
            }

            if (dragInterface != null && &candidate->AtkDragDropInterface == dragInterface)
            {
                slotIndex = i;
                slotComponent = candidate;
                return true;
            }
        }

        return false;
    }

    public static bool TryFindSlotByDragInterface(
        string gridAddonName,
        AtkDragDropInterface* dragInterface,
        out int uiSlotIndex,
        out AtkComponentDragDrop* slotComponent) =>
        TryFindSlotInGridAddon(gridAddonName, source: null, dragInterface, out uiSlotIndex, out slotComponent);

    public static uint TryGetComponentUiIconId(AtkComponentDragDrop* component)
    {
        if (component == null)
            return 0;

        return NativeQuickPanelUiReader.TryReadDragSourceAppearance(component, out var appearance, out _)
            ? appearance.IconId
            : 0;
    }

    public static string[] GetOrderedMainInventoryGridNames()
    {
        if (GenericHelpers.TryGetAddonByName<AddonInventoryExpansion>("InventoryExpansion", out var expansion)
            && GenericHelpers.IsAddonReady((AtkUnitBase*)expansion))
        {
            return
            [
                "InventoryGrid0E",
                "InventoryGrid1E",
                "InventoryGrid2E",
                "InventoryGrid3E",
                "InventoryGrid0",
                "InventoryGrid1",
                "InventoryGrid2",
                "InventoryGrid3",
            ];
        }

        return MainGridAddonNames;
    }

    public static bool TryMapMainInventoryGridName(string gridName, out InventoryType container)
    {
        container = default;

        const string prefix = "InventoryGrid";
        if (!gridName.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        var suffix = gridName[prefix.Length..];
        if (suffix.EndsWith('E'))
            suffix = suffix[..^1];

        if (!int.TryParse(suffix, out var bagIndex) || bagIndex is < 0 or > 3)
            return false;

        container = (InventoryType)bagIndex;
        return true;
    }
}

