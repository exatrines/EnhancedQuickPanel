using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EnhancedQuickPanel.Services;

/// <summary>Resolves the inventory source of an in-progress drag.</summary>
internal static unsafe class InventoryDragSourceResolver
{
    public static bool TryResolveFromManager(
        AtkDragDropManager* manager,
        out InventoryType container,
        out int slotIndex,
        out string sourceLabel)
    {
        container = default;
        slotIndex = -1;
        sourceLabel = string.Empty;

        if (!GameModuleGuard.IsClientReady || manager == null)
            return false;

        try
        {
            return TryResolveFromManagerCore(manager, out container, out slotIndex, out sourceLabel);
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"[EQP] Inventory drag resolve failed: {ex.Message}");
            return false;
        }
    }

    private static bool TryResolveFromManagerCore(
        AtkDragDropManager* manager,
        out InventoryType container,
        out int slotIndex,
        out string sourceLabel)
    {
        container = default;
        slotIndex = -1;
        sourceLabel = string.Empty;

        var dragInterface = manager->DragDrop2 != null ? manager->DragDrop2 : manager->DragDrop1;
        var referenceIndex = dragInterface != null ? dragInterface->DragDropReferenceIndex : (short)-1;

        var payload = dragInterface != null ? dragInterface->GetPayloadContainer() : null;
        if (payload == null)
            payload = &manager->PayloadContainer;

        if (payload->Int1 > 0
            && !InventorySlotHelper.IsMainInventoryType(payload->Int1)
            && TryFindBagHintByGridUiIcon(
                (uint)payload->Int1,
                referenceIndex,
                out var iconGridName,
                out var iconBag,
                out var iconUiSlot)
            && TryResolveFromIconAppearance(
                new ResolvedSlotIcon((uint)payload->Int1, false),
                quantity: null,
                iconBag,
                referenceIndex,
                iconGridName,
                iconUiSlot,
                out container,
                out slotIndex,
                out sourceLabel))
        {
            return true;
        }

        if (payload->Int1 > 0
            && InventorySlotHelper.TryFindInventorySlotByItemId(
                (uint)payload->Int1,
                preferredContainer: null,
                preferredSlot: null,
                referenceIndex,
                out container,
                out slotIndex,
                out _))
        {
            sourceLabel = $"payload item#{payload->Int1}";
            return true;
        }

        TryFindMainGridLocation(null, dragInterface, out var gridName, out var gridSlot, out var gridComponent);
        InventoryRemoteDragSourceRegistry.TryBuildDragHint(dragInterface, out var remoteDragHint);

        var preferredContainer = remoteDragHint.PreferredContainer;
        var labelUiGridSlot = remoteDragHint.LabelUiGridSlot;
        var labelSourceName = !string.IsNullOrWhiteSpace(remoteDragHint.LabelSourceName)
            ? remoteDragHint.LabelSourceName
            : gridName;

        if (preferredContainer == null
            && !string.IsNullOrWhiteSpace(gridName)
            && InventoryDragGridHelper.TryMapMainInventoryGridName(gridName, out var mappedContainer))
        {
            preferredContainer = mappedContainer;
            if (gridSlot >= 0)
                labelUiGridSlot = gridSlot;
        }

        if (payload->Int1 > 0
            && InventorySlotHelper.TryFindInventorySlotByItemId(
                (uint)payload->Int1,
                preferredContainer,
                preferredSlot: null,
                referenceIndex,
                out container,
                out slotIndex,
                out _))
        {
            sourceLabel = $"{labelSourceName} item#{payload->Int1}";
            return true;
        }

        AtkComponentDragDrop*[] sources =
        [
            TryGetDragDropFromInterface(dragInterface),
            remoteDragHint.SlotComponent,
            gridComponent,
            manager->DragDropS,
            TryGetFloatingPreviewComponent(),
        ];

        foreach (var source in sources)
        {
            if (source == null)
                continue;

            if (!TryResolveFromDragComponent(
                    source,
                    preferredContainer,
                    referenceIndex,
                    labelSourceName,
                    labelUiGridSlot,
                    out container,
                    out slotIndex,
                    out sourceLabel))
                continue;

            return true;
        }

        if (TryResolveFromPayload(
                payload->Int1,
                payload->Int2,
                referenceIndex,
                preferredContainer,
                labelSourceName,
                labelUiGridSlot,
                out container,
                out slotIndex,
                out sourceLabel))
            return true;

        if (InventoryRemoteDragSourceRegistry.TryResolveFromAnyActiveDragInterface(
                dragInterface,
                referenceIndex,
                out container,
                out slotIndex,
                out sourceLabel))
            return true;

        return TryFindSlotByActiveDragInterface(
            dragInterface,
            referenceIndex,
            out _,
            out _,
            out container,
            out slotIndex,
            out sourceLabel);
    }

    private static bool TryResolveFromPayload(
        int payloadInt1,
        int payloadInt2,
        short referenceIndex,
        InventoryType? preferredContainer,
        string gridName,
        int? labelUiGridSlot,
        out InventoryType container,
        out int slotIndex,
        out string sourceLabel)
    {
        container = default;
        slotIndex = -1;
        sourceLabel = string.Empty;

        if (payloadInt1 <= 0)
            return false;

        if (!InventorySlotHelper.IsMainInventoryType(payloadInt1))
        {
            var iconId = (uint)payloadInt1;
            var appearance = new ResolvedSlotIcon(iconId, false);

            if (TryResolveFromIconAppearance(
                    appearance,
                    quantity: null,
                    preferredContainer,
                    InventorySlotHelper.ResolveInventorySlotHint(referenceIndex, labelUiGridSlot),
                    gridName,
                    labelUiGridSlot,
                    out container,
                    out slotIndex,
                    out sourceLabel))
                return true;

            if (InventorySlotHelper.TryFindInventorySlotByItemId(
                    iconId,
                    preferredContainer,
                    preferredSlot: null,
                    referenceIndex,
                    out container,
                    out slotIndex,
                    out _))
            {
                sourceLabel = $"item#{iconId} inv-slot {slotIndex + 1}";
                return true;
            }

            if (InventorySlotHelper.TryResolveItemHotbarCommand(iconId, out _, out var commandId)
                && commandId != 0
                && preferredContainer is InventoryType hintedContainer
                && labelUiGridSlot is int _
                && InventorySlotHelper.TryResolveInventorySlotByDragIcon(
                    appearance,
                    quantity: null,
                    hintedContainer,
                    referenceIndex,
                    out container,
                    out slotIndex,
                    out _))
            {
                sourceLabel = $"payload item#{payloadInt1} inv-slot {slotIndex + 1}";
                return true;
            }
        }

        return false;
    }

    private static bool TryFindBagHintByGridUiIcon(
        uint iconId,
        short referenceIndex,
        out string gridName,
        out InventoryType bag,
        out int uiGridSlot)
    {
        gridName = string.Empty;
        bag = default;
        uiGridSlot = -1;

        if (iconId == 0)
            return false;

        var bestScore = int.MinValue;
        string? bestGridName = null;
        InventoryType? bestBag = null;
        var bestUiSlot = -1;

        foreach (var candidateGridName in InventoryDragGridHelper.GetOrderedMainInventoryGridNames())
        {
            try
            {
                if (!TryFindBagHintInGrid(candidateGridName, iconId, referenceIndex, ref bestScore, ref bestGridName, ref bestBag, ref bestUiSlot))
                    continue;
            }
            catch (Exception ex)
            {
                PluginLog.Debug($"[EQP] Grid bag-hint scan failed ({candidateGridName}): {ex.Message}");
            }
        }

        if (bestBag == null || bestGridName == null)
            return false;

        gridName = bestGridName;
        bag = bestBag.Value;
        uiGridSlot = bestUiSlot;
        return true;
    }

    private static bool TryFindBagHintInGrid(
        string candidateGridName,
        uint iconId,
        short referenceIndex,
        ref int bestScore,
        ref string? bestGridName,
        ref InventoryType? bestBag,
        ref int bestUiSlot)
    {
        if (!InventoryDragGridHelper.TryMapMainInventoryGridName(candidateGridName, out var candidateBag))
            return false;

        if (!GenericHelpers.TryGetAddonByName<AddonInventoryGrid>(candidateGridName, out var grid)
            || !GenericHelpers.IsAddonReady((AtkUnitBase*)grid))
            return false;

        var slots = grid->Slots;
        for (var i = 0; i < slots.Length; i++)
        {
            var component = slots[i].Value;
            if (component == null)
                continue;

            if (InventoryDragGridHelper.TryGetComponentUiIconId(component) != iconId)
                continue;

            var score = 0;
            if (InventorySlotHelper.IsPlausibleSlotIndexPublic(referenceIndex) && i == referenceIndex)
                score += 100;
            if (i == 0)
                score += 1;

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestGridName = candidateGridName;
            bestBag = candidateBag;
            bestUiSlot = i;
        }

        return bestBag != null;
    }

    private static bool TryFindSlotByActiveDragInterface(
        AtkDragDropInterface* dragInterface,
        short referenceIndex,
        out string gridName,
        out int uiGridSlot,
        out InventoryType container,
        out int slotIndex,
        out string sourceLabel)
    {
        gridName = string.Empty;
        uiGridSlot = -1;
        container = default;
        slotIndex = -1;
        sourceLabel = string.Empty;

        if (dragInterface == null)
            return false;

        foreach (var candidateGridName in InventoryDragGridHelper.GetOrderedMainInventoryGridNames())
        {
            try
            {
                if (!TryResolveActiveDragInGrid(
                        candidateGridName,
                        dragInterface,
                        referenceIndex,
                        out gridName,
                        out uiGridSlot,
                        out container,
                        out slotIndex,
                        out sourceLabel))
                    continue;

                return true;
            }
            catch (Exception ex)
            {
                PluginLog.Debug($"[EQP] Active drag resolve failed ({candidateGridName}): {ex.Message}");
            }
        }

        return false;
    }

    private static bool TryResolveActiveDragInGrid(
        string candidateGridName,
        AtkDragDropInterface* dragInterface,
        short referenceIndex,
        out string gridName,
        out int uiGridSlot,
        out InventoryType container,
        out int slotIndex,
        out string sourceLabel)
    {
        gridName = string.Empty;
        uiGridSlot = -1;
        container = default;
        slotIndex = -1;
        sourceLabel = string.Empty;

        if (!InventoryDragGridHelper.TryMapMainInventoryGridName(candidateGridName, out var bag))
            return false;

        if (!InventoryDragGridHelper.TryFindSlotByDragInterface(
                candidateGridName,
                dragInterface,
                out var uiSlot,
                out var component))
            return false;

        gridName = candidateGridName;
        uiGridSlot = uiSlot;

        if (!NativeQuickPanelUiReader.TryReadDragSourceAppearance(component, out var appearance, out var quantity)
            || appearance.IconId == 0)
            return false;

        return TryResolveFromIconAppearance(
            appearance,
            quantity,
            bag,
            InventorySlotHelper.ResolveInventorySlotHint(referenceIndex, uiGridSlot, bag),
            gridName,
            uiGridSlot,
            out container,
            out slotIndex,
            out sourceLabel);
    }

    private static AtkComponentDragDrop* TryGetFloatingPreviewComponent()
    {
        if (!GenericHelpers.TryGetAddonByName<AddonDragDrop>("DragDropS", out var dragPreviewAddon))
            return null;

        return NativeQuickPanelUiReader.TryFindDragDropComponent(
            (AtkUnitBase*)dragPreviewAddon,
            out var previewComponent)
            ? previewComponent
            : null;
    }

    private static bool TryResolveFromDragComponent(
        AtkComponentDragDrop* source,
        InventoryType? preferredContainer,
        short referenceIndex,
        string gridName,
        int? labelUiGridSlot,
        out InventoryType container,
        out int slotIndex,
        out string sourceLabel)
    {
        container = default;
        slotIndex = -1;
        sourceLabel = string.Empty;

        if (source == null)
            return false;

        if (!NativeQuickPanelUiReader.TryReadDragSourceAppearance(source, out var appearance, out var quantity)
            || appearance.IconId == 0)
            return false;

        InventoryType? bagHint = preferredContainer;
        string? labelGrid = string.IsNullOrWhiteSpace(gridName) ? null : gridName;
        int? uiSlotHint = labelUiGridSlot;

        if (TryFindBagHintByGridUiIcon(
                appearance.IconId,
                referenceIndex,
                out var iconGridName,
                out var iconBag,
                out var iconUiSlot))
        {
            bagHint = iconBag;
            labelGrid = iconGridName;
            uiSlotHint = iconUiSlot;
        }

        return TryResolveFromIconAppearance(
            appearance,
            quantity,
            bagHint,
            InventorySlotHelper.ResolveInventorySlotHint(referenceIndex, uiSlotHint, bagHint),
            labelGrid,
            uiSlotHint,
            out container,
            out slotIndex,
            out sourceLabel);
    }

    internal static bool TryResolveFromIconAppearance(
        ResolvedSlotIcon appearance,
        int? quantity,
        InventoryType? preferredContainer,
        short referenceIndex,
        string? labelGridName,
        int? labelUiGridSlot,
        out InventoryType container,
        out int slotIndex,
        out string sourceLabel)
    {
        container = default;
        slotIndex = -1;
        sourceLabel = string.Empty;

        var slotHint = InventorySlotHelper.ResolveInventorySlotHint(referenceIndex, labelUiGridSlot, preferredContainer);

        if (preferredContainer is InventoryType bag
            && labelUiGridSlot is int uiSlot
            && InventorySlotHelper.IsPlausibleSlotIndexForContainer(bag, uiSlot)
            && InventoryDragSafety.TryVerifyDragResolution(appearance, bag, uiSlot))
        {
            container = bag;
            slotIndex = uiSlot;
            return TryBuildIconSourceLabel(labelGridName, labelUiGridSlot, appearance, container, slotIndex, out sourceLabel);
        }

        if (!InventorySlotHelper.TryResolveInventorySlotByDragIcon(
                appearance,
                quantity,
                preferredContainer,
                slotHint,
                out container,
                out slotIndex,
                out _))
            return false;

        return TryBuildIconSourceLabel(labelGridName, labelUiGridSlot, appearance, container, slotIndex, out sourceLabel);
    }

    private static bool TryBuildIconSourceLabel(
        string? labelGridName,
        int? labelUiGridSlot,
        ResolvedSlotIcon appearance,
        InventoryType container,
        int slotIndex,
        out string sourceLabel)
    {
        sourceLabel = string.Empty;

        var hqLabel = appearance.IsHighQuality ? " hq" : string.Empty;
        if (!string.IsNullOrWhiteSpace(labelGridName) && labelUiGridSlot is int uiSlot)
        {
            sourceLabel =
                $"{labelGridName}#{uiSlot + 1} inv-slot#{slotIndex + 1} source-icon#{appearance.IconId}{hqLabel}";
            return true;
        }

        sourceLabel = $"source-icon#{appearance.IconId} inv-slot#{slotIndex + 1}{hqLabel}";
        return true;
    }

    private static AtkComponentDragDrop* TryGetDragDropFromInterface(AtkDragDropInterface* dragInterface)
    {
        if (dragInterface == null)
            return null;

        var component = dragInterface->GetComponent();
        if (component == null || component->GetComponentType() != ComponentType.DragDrop)
            return null;

        return (AtkComponentDragDrop*)component;
    }

    private static bool TryFindMainGridLocation(
        AtkComponentDragDrop* source,
        AtkDragDropInterface* dragInterface,
        out string gridName,
        out int slotIndex,
        out AtkComponentDragDrop* slotComponent)
    {
        gridName = string.Empty;
        slotIndex = -1;
        slotComponent = null;

        foreach (var candidateGridName in InventoryDragGridHelper.GetOrderedMainInventoryGridNames())
        {
            if (!InventoryDragGridHelper.TryFindSlotInGridAddon(
                    candidateGridName,
                    source,
                    dragInterface,
                    out slotIndex,
                    out slotComponent))
                continue;

            gridName = candidateGridName;
            return true;
        }

        return false;
    }
}

