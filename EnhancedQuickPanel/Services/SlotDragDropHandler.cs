using EnhancedQuickPanel.Models;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EnhancedQuickPanel.Services;

/// <summary>Handles dragging game actions/items onto slots and activating slots via clicks.</summary>
internal static unsafe class SlotDragDropHandler
{
    private static bool _wasDragging;
    private static int _hoverPage = -1;
    private static int _hoverSlotIndex = -1;
    private static int _lastAppliedPage = -1;
    private static int _lastAppliedSlotIndex = -1;
    private static bool _suppressSlotClickUntilMouseUp;

    public static void NotifySlotHover(int page, int slotIndex, bool hovered)
    {
        if (hovered)
        {
            _hoverPage = page;
            _hoverSlotIndex = slotIndex;
            return;
        }

        if (_wasDragging || IsGameDragging)
            return;

        if (_hoverPage == page && _hoverSlotIndex == slotIndex)
        {
            _hoverPage = -1;
            _hoverSlotIndex = -1;
        }
    }

    public static bool TryHandleDropOnClick(int page, int slotIndex, PanelSlot slot)
    {
        if (!GameModuleGuard.IsClientReady)
            return false;

        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            return false;

        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            return false;

        var stage = AtkStage.Instance();
        if (stage == null || !stage->DragDropManager.IsDragging)
            return false;

        if (!TryReadActiveDrag(stage, out var snapshot))
            return false;

        NotifySlotHover(page, slotIndex, true);

        LogDragGrab(snapshot);

        if (!TryApplyToPanelSlot(slot, snapshot))
            return false;

        LogAppliedDrop(slot, snapshot, page, slotIndex);

        EzConfig.Save();
        stage->DragDropManager.CancelDragDrop(allowSoundEffect: true, suppressFlyBack: true);
        RememberAppliedDrop(page, slotIndex);
        return true;
    }

    public static bool TryActivateSlot(int page, int slotIndex)
    {
        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            return false;

        if (!ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            return false;

        if (ShouldSuppressSlotClick(page, slotIndex))
            return false;

        if (IsGameDragging || _wasDragging || SlotSwapDragHandler.ShouldSuppressSlotActivation())
            return false;

        return true;
    }

    public static bool ShouldSuppressSlotClick(int page, int slotIndex)
    {
        if (_suppressSlotClickUntilMouseUp
            && _lastAppliedPage == page
            && _lastAppliedSlotIndex == slotIndex)
            return true;

        if (IsGameDragging || _wasDragging || SlotSwapDragHandler.ShouldSuppressSlotActivation())
            return page == _hoverPage && slotIndex == _hoverSlotIndex;

        return false;
    }

    public static bool ShouldHighlightDropTarget(int page, int slotIndex)
    {
        if (page != _hoverPage || slotIndex != _hoverSlotIndex)
            return false;

        var stage = AtkStage.Instance();
        if (stage == null || !stage->DragDropManager.IsDragging)
            return false;

        return TryReadActiveDrag(stage, out _);
    }

    public static void ProcessEndOfFrame()
    {
        var stage = AtkStage.Instance();
        var isDragging = stage != null && stage->DragDropManager.IsDragging;

        if (!isDragging && !_wasDragging)
        {
            _hoverPage = -1;
            _hoverSlotIndex = -1;
        }

        if (_suppressSlotClickUntilMouseUp && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            _suppressSlotClickUntilMouseUp = false;

        _wasDragging = isDragging;
    }

    private static bool IsGameDragging
    {
        get
        {
            var stage = AtkStage.Instance();
            return stage != null && stage->DragDropManager.IsDragging;
        }
    }

    public static bool IsGameDragActive => IsGameDragging;

    private static void RememberAppliedDrop(int page, int slotIndex)
    {
        _lastAppliedPage = page;
        _lastAppliedSlotIndex = slotIndex;
        _suppressSlotClickUntilMouseUp = true;
    }

    private static void LogDragGrab(DragSnapshot snapshot)
    {
        PluginLog.Debug(
            $"[EQP] Drag grab click: drag={snapshot.DragType}, payload Int1={snapshot.Int1}, Int2={snapshot.Int2}, ref={snapshot.ReferenceIndex}, macroName={snapshot.MacroName}, ui={snapshot.UiSourceLabel}");

        if (TryDescribeGrabbedPayload(snapshot, out var description))
            PluginLog.Debug($"[EQP] Drag grab resolved: {description}");
        else
            PluginLog.Debug("[EQP] Drag grab resolved: (could not resolve payload)");
    }

    private static void LogAppliedDrop(PanelSlot slot, DragSnapshot snapshot, int page, int slotIndex)
    {
        switch (slot.Kind)
        {
            case PanelSlotKind.Macro:
                PluginLog.Debug(
                    $"[EQP] Drop applied: page={page + 1}, slot={slotIndex + 1}, kind=Macro, set={slot.MacroSet}, index={slot.MacroIndex}, commandId={slot.CommandId}");
                return;

            case PanelSlotKind.Action:
            {
                var slotType = (RaptureHotbarModule.HotbarSlotType)slot.CommandType;
                if (InventorySlotHelper.IsItemSlotType(slotType))
                {
                    var (baseItemId, isHq) = InventorySlotHelper.DecodeItemId(slot.CommandId);
                    PluginLog.Debug(
                        $"[EQP] Drop applied item: page={page + 1}, slot={slotIndex + 1}, drag={snapshot.DragType}, type={slotType}, commandId={slot.CommandId}, itemId={baseItemId}, hq={isHq}");
                    return;
                }

                PluginLog.Debug(
                    $"[EQP] Drop applied: page={page + 1}, slot={slotIndex + 1}, drag={snapshot.DragType}, type={slotType}, commandId={slot.CommandId}");
                return;
            }

            case PanelSlotKind.TextCommand:
                PluginLog.Debug(
                    $"[EQP] Drop applied: page={page + 1}, slot={slotIndex + 1}, kind=TextCommand");
                return;
        }
    }

    private static bool TryDescribeGrabbedPayload(DragSnapshot snapshot, out string description)
    {
        description = string.Empty;

        if (IsActionBarDragType(snapshot.DragType)
            && TryReadActionBarSource(snapshot, out var hotbarType, out var hotbarId))
        {
            description = FormatGrabDescription("actionbar", hotbarType, hotbarId);
            return true;
        }

        if (snapshot.DragType is DragDropType.Inventory_Item or DragDropType.RemoteInventory_Item)
        {
            if (InventorySlotHelper.TryResolveDraggedInventoryItem(
                    snapshot.DragType,
                    snapshot.Int1,
                    snapshot.Int2,
                    snapshot.ReferenceIndex,
                    out var itemType,
                    out var itemId))
            {
                description = FormatGrabDescription(
                    $"inventory({(InventoryType)snapshot.Int1}#{snapshot.Int2}, ref={snapshot.ReferenceIndex})",
                    itemType,
                    itemId);
                return true;
            }

            if (InventorySlotHelper.TryResolveDraggedInventoryLocation(
                    snapshot.DragType,
                    snapshot.Int1,
                    snapshot.Int2,
                    snapshot.ReferenceIndex,
                    out var container,
                    out var slot))
            {
                description =
                    $"inventory location {container}#{slot} (ref={snapshot.ReferenceIndex}), item unresolved";
                return true;
            }

            description =
                $"inventory payload Int1={snapshot.Int1}, Int2={snapshot.Int2}, ref={snapshot.ReferenceIndex} unresolved";
            return true;
        }

        if (snapshot.DragType == DragDropType.Inventory_Crystal)
        {
            if (InventorySlotHelper.TryResolveDraggedInventoryItem(
                    snapshot.DragType,
                    snapshot.Int1,
                    snapshot.Int2,
                    snapshot.ReferenceIndex,
                    out var itemType,
                    out var itemId))
            {
                description = FormatGrabDescription($"crystal#{snapshot.Int2}", itemType, itemId);
                return true;
            }

            description = $"crystal payload (slot={snapshot.Int2}, ref={snapshot.ReferenceIndex}) unresolved";
            return true;
        }

        if (snapshot.DragType is DragDropType.Item or DragDropType.ActionBar_Item or DragDropType.EventItem)
        {
            if (TryMapInventoryDrag(snapshot, out var mappedType, out var mappedId))
            {
                description = FormatGrabDescription(
                    string.IsNullOrWhiteSpace(snapshot.UiSourceLabel)
                        ? "inventory"
                        : snapshot.UiSourceLabel,
                    mappedType,
                    mappedId);
                return true;
            }

            description =
                $"inventory unresolved: Int1={snapshot.Int1}, Int2={snapshot.Int2}, ref={snapshot.ReferenceIndex}";
            return true;
        }

        if (TryMapInventoryDrag(snapshot, out var inventoryMappedType, out var inventoryMappedId))
        {
            description = FormatGrabDescription(
                $"inventory-mapped({snapshot.UiSourceLabel})",
                inventoryMappedType,
                inventoryMappedId);
            return true;
        }

        var (initialType, initialId) = MapDragToHotbar(snapshot);
        if (initialType == RaptureHotbarModule.HotbarSlotType.Empty || initialId == 0)
            return false;

        description = FormatGrabDescription("payload-mapped", initialType, initialId);
        return true;
    }

    private static string FormatGrabDescription(
        string source,
        RaptureHotbarModule.HotbarSlotType type,
        uint commandId)
    {
        if (InventorySlotHelper.IsItemSlotType(type))
        {
            var (baseItemId, isHq) = InventorySlotHelper.DecodeItemId(commandId);
            return $"{source}, type={type}, commandId={commandId}, itemId={baseItemId}, hq={isHq}";
        }

        if (type == RaptureHotbarModule.HotbarSlotType.Macro)
        {
            if (MacroSlotResolver.TryResolveMacroSlot(commandId, out var macroSet, out var macroIndex))
                return $"{source}, macro set={macroSet}, index={macroIndex}, commandId={commandId}";

            return $"{source}, macro commandId={commandId} unresolved";
        }

        return $"{source}, type={type}, commandId={commandId}";
    }

    private static bool TryReadActiveDrag(AtkStage* stage, out DragSnapshot snapshot)
    {
        snapshot = default;

        try
        {
            var manager = &stage->DragDropManager;
            if (!manager->IsDragging)
                return false;

            var dragInterface = manager->DragDrop2 != null ? manager->DragDrop2 : manager->DragDrop1;
            if (dragInterface == null)
                return false;

            var dragType = dragInterface->DragDropType;
            if (!IsSupportedDragType(dragType))
                return false;

            var payload = dragInterface->GetPayloadContainer();
            if (payload == null)
                payload = &manager->PayloadContainer;

            var uiSourceLabel = InventoryDragSourceResolver.TryResolveFromManager(
                manager,
                out _,
                out _,
                out var label)
                ? label
                : string.Empty;

            var macroName = dragType == DragDropType.Macro
                ? SeStringTextHelper.ReadPlainText(payload->Text)
                : string.Empty;

            snapshot = new DragSnapshot(
                dragType,
                payload->Int1,
                payload->Int2,
                dragInterface->DragDropReferenceIndex,
                uiSourceLabel,
                macroName);
            return true;
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"[EQP] Active drag read failed: {ex.Message}");
            return false;
        }
    }

    private static bool TryApplyToPanelSlot(PanelSlot slot, DragSnapshot snapshot)
    {
        if (snapshot.DragType == DragDropType.Macro
            && MacroSlotResolver.TryResolveMacroDragPayload(
                snapshot.Int1,
                snapshot.Int2,
                snapshot.ReferenceIndex,
                snapshot.MacroName,
                out var macroSet,
                out var macroIndex))
        {
            MacroSlotResolver.ApplyMacroSlot(slot, macroSet, macroIndex);
            return slot.IsConfigured;
        }

        if (!GameModuleGuard.TryGetHotbar(out var hotbar, out var uiModule))
            return false;

        var (initialType, initialId) = MapDragToHotbar(snapshot);
        if (initialType == RaptureHotbarModule.HotbarSlotType.Empty || initialId == 0)
            return false;

        try
        {
            var scratch = hotbar->ScratchSlot;
            scratch.Set(uiModule, initialType, initialId);

            var resolvedType = scratch.CommandType;
            var resolvedId = scratch.CommandId;
            if (resolvedType == RaptureHotbarModule.HotbarSlotType.Empty || resolvedId == 0)
                return false;

            if (InventorySlotHelper.TryResolveInventoryLinkedCommand(resolvedType, resolvedId, out var itemType, out var itemId))
            {
                resolvedType = itemType;
                resolvedId = itemId;
            }

            ApplyResolvedSlot(slot, resolvedType, resolvedId);
            return slot.IsConfigured;
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"[EQP] Drag-drop apply failed: {ex.Message}");
            return false;
        }
    }

    private static void ApplyResolvedSlot(
        PanelSlot slot,
        RaptureHotbarModule.HotbarSlotType resolvedType,
        uint resolvedId)
    {
        slot.IconId = 0;
        slot.Label = string.Empty;
        slot.TextBody = string.Empty;

        if (resolvedType == RaptureHotbarModule.HotbarSlotType.Macro)
        {
            if (MacroSlotResolver.TryResolveMacroSlot(resolvedId, out var macroSet, out var macroIndex))
            {
                MacroSlotResolver.ApplyMacroSlot(slot, macroSet, macroIndex);
                return;
            }

            return;
        }

        var imported = NativeQuickPanelImporter.FromHotbarSlot(resolvedType, resolvedId);
        slot.Kind = imported.Kind;
        slot.CommandType = imported.CommandType;
        slot.CommandId = imported.CommandId;
        slot.MacroSet = imported.MacroSet;
        slot.MacroIndex = imported.MacroIndex;
    }

    private static (RaptureHotbarModule.HotbarSlotType Type, uint CommandId) MapDragToHotbar(DragSnapshot snapshot)
    {
        if (IsActionBarDragType(snapshot.DragType)
            && TryReadActionBarSource(snapshot, out var sourceType, out var sourceId))
            return (sourceType, sourceId);

        if (TryMapInventoryDrag(snapshot, out var inventoryType, out var inventoryId))
            return (inventoryType, inventoryId);

        switch (snapshot.DragType)
        {
            case DragDropType.Macro:
                if (MacroSlotResolver.TryResolveMacroDragPayload(
                        snapshot.Int1,
                        snapshot.Int2,
                        snapshot.ReferenceIndex,
                        snapshot.MacroName,
                        out var macroSet,
                        out var macroIndex))
                {
                    return (
                        RaptureHotbarModule.HotbarSlotType.Macro,
                        MacroSlotResolver.EncodeHotbarCommandId(macroSet, macroIndex));
                }

                return default;

            case DragDropType.ActionBar_Macro:
                return (RaptureHotbarModule.HotbarSlotType.Macro, (uint)Math.Max(0, snapshot.Int2));

            case DragDropType.Action:
            case DragDropType.ActionBar_Action:
                return (RaptureHotbarModule.HotbarSlotType.Action, (uint)Math.Max(0, snapshot.Int2));

            case DragDropType.Marker:
            case DragDropType.ActionBar_Marker:
                return (RaptureHotbarModule.HotbarSlotType.Marker, (uint)Math.Max(0, snapshot.Int2));

            case DragDropType.FieldMarker:
            case DragDropType.ActionBar_FieldMarker:
                return (RaptureHotbarModule.HotbarSlotType.FieldMarker, (uint)Math.Max(0, snapshot.Int2));

            default:
                var slotType = UIGlobals.GetHotbarSlotTypeFromDragDropType(snapshot.DragType);
                if (slotType == RaptureHotbarModule.HotbarSlotType.Empty)
                    return default;

                if (slotType is RaptureHotbarModule.HotbarSlotType.InventoryItem
                    or RaptureHotbarModule.HotbarSlotType.Crystal
                    or RaptureHotbarModule.HotbarSlotType.KeyItem)
                {
                    if (!InventorySlotHelper.IsDragResolvableInventoryType(snapshot.Int1))
                        return default;

                    var container = (InventoryType)snapshot.Int1;
                    int slot;
                    if (InventorySlotHelper.IsPlausibleSlotIndexForContainer(container, snapshot.Int2))
                        slot = snapshot.Int2;
                    else if (InventorySlotHelper.IsPlausibleSlotIndexForContainer(container, snapshot.ReferenceIndex))
                        slot = snapshot.ReferenceIndex;
                    else
                        return default;

                    return (
                        slotType,
                        InventorySlotHelper.EncodeInventoryCommand(container, slot));
                }

                return (slotType, (uint)Math.Max(0, snapshot.Int2));
        }
    }

    private static bool TryMapInventoryDrag(
        DragSnapshot snapshot,
        out RaptureHotbarModule.HotbarSlotType type,
        out uint commandId)
    {
        type = RaptureHotbarModule.HotbarSlotType.Empty;
        commandId = 0;

        if (TryResolveInventoryDragFromUi(snapshot, out type, out commandId))
            return true;

        if (snapshot.DragType == DragDropType.Item)
            return false;

        switch (snapshot.DragType)
        {
            case DragDropType.Inventory_Item:
            case DragDropType.RemoteInventory_Item:
            case DragDropType.Inventory_Crystal:
            case DragDropType.Item:
            case DragDropType.ActionBar_Item:
            case DragDropType.EventItem:
            case DragDropType.ActionBar_EventItem:
                if (InventorySlotHelper.TryResolveDraggedInventoryItem(
                        snapshot.DragType,
                        snapshot.Int1,
                        snapshot.Int2,
                        snapshot.ReferenceIndex,
                        out type,
                        out commandId))
                    return true;

                if (InventorySlotHelper.TryResolveDraggedInventoryLocation(
                        snapshot.DragType,
                        snapshot.Int1,
                        snapshot.Int2,
                        snapshot.ReferenceIndex,
                        out var container,
                        out var slot))
                {
                    type = RaptureHotbarModule.HotbarSlotType.InventoryItem;
                    commandId = InventorySlotHelper.EncodeInventoryCommand(container, slot);
                    return true;
                }

                if (InventorySlotHelper.LooksLikeDirectItemId(snapshot.Int2)
                    && InventorySlotHelper.TryResolveItemHotbarCommand(
                        (uint)snapshot.Int2,
                        out type,
                        out commandId))
                    return true;

                if (InventorySlotHelper.LooksLikeDirectItemId(snapshot.Int1)
                    && InventorySlotHelper.TryResolveItemHotbarCommand(
                        (uint)snapshot.Int1,
                        out type,
                        out commandId))
                    return true;

                return false;

            default:
                return false;
        }
    }

    private static bool TryResolveInventoryDragFromUi(
        DragSnapshot snapshot,
        out RaptureHotbarModule.HotbarSlotType type,
        out uint commandId)
    {
        type = RaptureHotbarModule.HotbarSlotType.Empty;
        commandId = 0;

        if (snapshot.DragType is not (DragDropType.Item
            or DragDropType.Inventory_Item
            or DragDropType.RemoteInventory_Item
            or DragDropType.Inventory_Crystal
            or DragDropType.ActionBar_Item
            or DragDropType.EventItem
            or DragDropType.ActionBar_EventItem))
            return false;

        var stage = AtkStage.Instance();
        if (stage == null)
            return false;

        if (!InventoryDragSourceResolver.TryResolveFromManager(
                &stage->DragDropManager,
                out var container,
                out var slot,
                out _))
            return false;

        if (InventorySlotHelper.TryResolveInventoryLocation(container, slot, out type, out commandId))
            return true;

        return TryApplyInventoryLocationFallback(container, slot, out type, out commandId);
    }

    private static bool TryApplyInventoryLocationFallback(
        InventoryType container,
        int slot,
        out RaptureHotbarModule.HotbarSlotType type,
        out uint commandId)
    {
        type = RaptureHotbarModule.HotbarSlotType.Empty;
        commandId = 0;

        if (!InventorySlotHelper.IsDragResolvableInventoryType((int)container)
            || !InventorySlotHelper.IsPlausibleSlotIndexForContainer(container, slot)
            || !InventoryDragSafety.HasInventoryItemAt(container, slot))
            return false;

        type = RaptureHotbarModule.HotbarSlotType.InventoryItem;
        commandId = InventorySlotHelper.EncodeInventoryCommand(container, slot);
        return true;
    }

    private static bool TryReadActionBarSource(
        DragSnapshot snapshot,
        out RaptureHotbarModule.HotbarSlotType type,
        out uint commandId)
    {
        type = RaptureHotbarModule.HotbarSlotType.Empty;
        commandId = 0;

        if (!GameModuleGuard.TryGetHotbar(out var hotbar, out _))
            return false;

        var hotbarId = (uint)Math.Max(0, snapshot.Int1);
        var slotId = (uint)Math.Max(0, (int)snapshot.ReferenceIndex);
        var sourceSlot = hotbar->GetSlotById(hotbarId, slotId);
        if (sourceSlot == null || sourceSlot->IsEmpty)
            return false;

        type = sourceSlot->CommandType;
        commandId = sourceSlot->CommandId;
        return type != RaptureHotbarModule.HotbarSlotType.Empty && commandId != 0;
    }

    private static bool IsActionBarDragType(DragDropType dragType) =>
        dragType is >= DragDropType.ActionBar_Macro and <= DragDropType.ActionBar_Glasses
            or DragDropType.ActionBar;

    private static bool IsSupportedDragType(DragDropType dragType) =>
        dragType is DragDropType.Item
            or DragDropType.Action
            or DragDropType.Macro
            or DragDropType.EventItem
            or DragDropType.Marker
            or DragDropType.FieldMarker
            or DragDropType.Inventory_Item
            or DragDropType.Inventory_Crystal
            or DragDropType.ActionBar_Item
            or DragDropType.ActionBar_Action
            or DragDropType.ActionBar_Macro
            or DragDropType.ActionBar_EventItem
            or DragDropType.ActionBar_Marker
            or DragDropType.ActionBar_FieldMarker
            or DragDropType.Emote
            or DragDropType.ActionBar_Emote
            or DragDropType.GeneralAction
            or DragDropType.ActionBar_GeneralAction
            or DragDropType.CraftingAction
            or DragDropType.ActionBar_CraftingAction
            or DragDropType.Mount
            or DragDropType.ActionBar_Mount
            or DragDropType.MainCommand
            or DragDropType.ActionBar_MainCommand
            or DragDropType.GearSet
            or DragDropType.ActionBar_GearSet
            or DragDropType.Companion
            or DragDropType.ActionBar_Companion
            or DragDropType.BuddyAction
            or DragDropType.ActionBar_BuddyAction
            or DragDropType.PetAction
            or DragDropType.ActionBar_PetAction
            or DragDropType.ActionBar;

    /// <summary>Snapshot of an in-progress game drag operation.</summary>
    private readonly record struct DragSnapshot(
        DragDropType DragType,
        int Int1,
        int Int2,
        short ReferenceIndex,
        string UiSourceLabel,
        string MacroName);
}
