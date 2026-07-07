using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EnhancedQuickPanel.Services;

/// <summary>Resolves items dragged from remote inventories.</summary>
internal static unsafe class InventoryRemoteDragResolver
{
    public static bool TryResolveFromLocatedSlot(
        AtkComponentDragDrop* component,
        short referenceIndex,
        InventoryType container,
        int inventorySlot,
        string labelSourceName,
        out InventoryType resolvedContainer,
        out int resolvedSlot,
        out string sourceLabel)
    {
        resolvedContainer = default;
        resolvedSlot = -1;
        sourceLabel = string.Empty;

        if (component == null)
            return false;

        if (!NativeQuickPanelUiReader.TryReadDragSourceAppearance(component, out var appearance, out var quantity)
            || appearance.IconId == 0)
            return false;

        return InventoryDragSourceResolver.TryResolveFromIconAppearance(
            appearance,
            quantity,
            container,
            InventorySlotHelper.ResolveInventorySlotHint(referenceIndex, inventorySlot, container),
            labelSourceName,
            inventorySlot,
            out resolvedContainer,
            out resolvedSlot,
            out sourceLabel);
    }
}

