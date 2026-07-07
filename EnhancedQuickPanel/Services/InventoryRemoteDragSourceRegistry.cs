using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EnhancedQuickPanel.Services;

/// <summary>Tracks remote (retainer/buddy) inventory drag sources.</summary>
internal static unsafe class InventoryRemoteDragSourceRegistry
{
    private delegate bool DragHintProbe(
        AtkDragDropInterface* dragInterface,
        out InventoryDragSourceHint hint);

    private delegate bool ActiveDragProbe(
        AtkDragDropInterface* dragInterface,
        short referenceIndex,
        out InventoryType container,
        out int slotIndex,
        out string sourceLabel);

    private static readonly DragHintProbe[] HintProbes =
    [
        InventoryBuddyDragSourceResolver.TryBuildDragHint,
        InventoryArmouryDragSourceResolver.TryBuildDragHint,
        InventoryRetainerDragSourceResolver.TryBuildDragHint,
    ];

    private static readonly ActiveDragProbe[] ActiveProbes =
    [
        InventoryBuddyDragSourceResolver.TryResolveFromActiveDragInterface,
        InventoryArmouryDragSourceResolver.TryResolveFromActiveDragInterface,
        InventoryRetainerDragSourceResolver.TryResolveFromActiveDragInterface,
    ];

    public static bool TryBuildDragHint(
        AtkDragDropInterface* dragInterface,
        out InventoryDragSourceHint hint)
    {
        hint = InventoryDragSourceHint.Empty;

        if (dragInterface == null)
            return false;

        foreach (var probe in HintProbes)
        {
            if (!probe(dragInterface, out hint))
                continue;

            return hint.HasMapping;
        }

        return false;
    }

    public static bool TryResolveFromAnyActiveDragInterface(
        AtkDragDropInterface* dragInterface,
        short referenceIndex,
        out InventoryType container,
        out int slotIndex,
        out string sourceLabel)
    {
        container = default;
        slotIndex = -1;
        sourceLabel = string.Empty;

        if (dragInterface == null)
            return false;

        foreach (var probe in ActiveProbes)
        {
            if (probe(dragInterface, referenceIndex, out container, out slotIndex, out sourceLabel))
                return true;
        }

        return false;
    }
}

