using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EnhancedQuickPanel.Services;

/// <summary>Hint describing where a dragged inventory item came from.</summary>
internal unsafe struct InventoryDragSourceHint
{
    public InventoryType? PreferredContainer;
    public int? LabelUiGridSlot;
    public string LabelSourceName;
    public AtkComponentDragDrop* SlotComponent;

    public static InventoryDragSourceHint Empty => default;

    public readonly bool HasMapping => PreferredContainer is not null && LabelUiGridSlot is not null;
}

