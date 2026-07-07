using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EnhancedQuickPanel.Services;

/// <summary>Resolves drag sources originating from retainer inventories.</summary>
internal static unsafe class InventoryRetainerDragSourceResolver
{
    internal const string GridPrefix = "InventoryRetainerGrid";
    internal const string GridAliasPrefix = "RetainerGrid";
    internal const int PageCount = 7;
    internal const int UiSlotCount = 35;

    public static bool TryBuildDragHint(
        AtkDragDropInterface* dragInterface,
        out InventoryDragSourceHint hint)
    {
        hint = InventoryDragSourceHint.Empty;

        if (!TryFindRetainerLocation(dragInterface, out var gridName, out var uiSlotIndex, out var component)
            || !TryMapRetainerGridUiSlot(gridName, uiSlotIndex, out var container, out var inventorySlot))
            return false;

        hint = new InventoryDragSourceHint
        {
            PreferredContainer = container,
            LabelUiGridSlot = inventorySlot,
            LabelSourceName = $"{gridName}#{uiSlotIndex + 1}",
            SlotComponent = component,
        };
        return true;
    }

    public static bool TryFindRetainerLocation(
        AtkDragDropInterface* dragInterface,
        out string gridName,
        out int uiSlotIndex,
        out AtkComponentDragDrop* slotComponent)
    {
        gridName = string.Empty;
        uiSlotIndex = -1;
        slotComponent = null;

        if (dragInterface == null)
            return false;

        foreach (var candidateGridName in GetOrderedGridNames())
        {
            if (!InventoryDragGridHelper.TryFindSlotByDragInterface(
                    candidateGridName,
                    dragInterface,
                    out uiSlotIndex,
                    out slotComponent))
                continue;

            gridName = candidateGridName;
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

        if (!TryFindRetainerLocation(dragInterface, out var gridName, out var uiSlotIndex, out var component)
            || !TryMapRetainerGridUiSlot(gridName, uiSlotIndex, out container, out var inventorySlot))
            return false;

        return InventoryRemoteDragResolver.TryResolveFromLocatedSlot(
            component,
            referenceIndex,
            container,
            inventorySlot,
            $"{gridName}#{uiSlotIndex + 1}",
            out container,
            out slotIndex,
            out sourceLabel);
    }

    public static bool TryMapRetainerGridUiSlot(
        string gridName,
        int uiSlotIndex,
        out InventoryType container,
        out int inventorySlot)
    {
        container = default;
        inventorySlot = -1;

        if (uiSlotIndex < 0 || uiSlotIndex >= UiSlotCount)
            return false;

        if (!TryMapRetainerGridNameToInventoryType(gridName, out container))
            return false;

        inventorySlot = uiSlotIndex;
        return InventorySlotHelper.IsPlausibleSlotIndexForContainer(container, inventorySlot);
    }

    public static bool TryMapRetainerGridNameToInventoryType(string gridName, out InventoryType container)
    {
        container = default;

        string? suffix = null;
        if (gridName.StartsWith(GridPrefix, StringComparison.Ordinal))
            suffix = gridName[GridPrefix.Length..];
        else if (gridName.StartsWith(GridAliasPrefix, StringComparison.Ordinal))
            suffix = gridName[GridAliasPrefix.Length..];

        if (string.IsNullOrEmpty(suffix))
            return false;

        if (suffix.EndsWith('E'))
            suffix = suffix[..^1];

        if (!int.TryParse(suffix, out var pageIndex) || pageIndex is < 0 or >= PageCount)
            return false;

        container = (InventoryType)((int)InventoryType.RetainerPage1 + pageIndex);
        return true;
    }

    private static string[] GetOrderedGridNames()
    {
        if (GenericHelpers.TryGetAddonByName<AddonInventoryRetainerLarge>("InventoryRetainerLarge", out var large)
            && GenericHelpers.IsAddonReady((AtkUnitBase*)large))
        {
            return
            [
                $"{GridPrefix}0E",
                $"{GridPrefix}1E",
                $"{GridPrefix}2E",
                $"{GridPrefix}3E",
                $"{GridPrefix}4E",
                $"{GridPrefix}5E",
                $"{GridPrefix}6E",
                $"{GridPrefix}0",
                $"{GridPrefix}1",
                $"{GridPrefix}2",
                $"{GridPrefix}3",
                $"{GridPrefix}4",
                $"{GridPrefix}5",
                $"{GridPrefix}6",
            ];
        }

        return
        [
            $"{GridPrefix}0",
            $"{GridPrefix}1",
            $"{GridPrefix}2",
            $"{GridPrefix}3",
            $"{GridPrefix}4",
            $"{GridPrefix}5",
            $"{GridPrefix}6",
        ];
    }
}

