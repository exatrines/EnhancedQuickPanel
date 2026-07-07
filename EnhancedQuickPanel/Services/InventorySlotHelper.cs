using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EnhancedQuickPanel.Services;

/// <summary>Resolves inventory item locations and details used for slot drag and drop.</summary>
internal static unsafe class InventorySlotHelper
{
    public const uint HqItemIdOffset = 1_000_000;
    private const int MaxSymbolicDepth = 4;
    private const int MaxInventoryGridSlotIndex = 34;
    private const int MaxArmoryMainHandSlotIndex = 49;
    private const int MaxRetainerPageSlotIndex = 24;

    public static (uint BaseItemId, bool IsHighQuality) DecodeItemId(uint itemId) =>
        itemId >= HqItemIdOffset
            ? (itemId - HqItemIdOffset, true)
            : (itemId, false);

    public static bool IsItemSlotType(RaptureHotbarModule.HotbarSlotType type) =>
        type is RaptureHotbarModule.HotbarSlotType.Item
            or RaptureHotbarModule.HotbarSlotType.EventItem
            or RaptureHotbarModule.HotbarSlotType.InventoryItem
            or RaptureHotbarModule.HotbarSlotType.Crystal
            or RaptureHotbarModule.HotbarSlotType.KeyItem;

    public static InventoryItem* TryGetLinkedInventoryItem(
        RaptureHotbarModule.HotbarSlotType type,
        uint commandId)
    {
        if (!GameModuleGuard.TryGetInventory(out var inventory))
            return null;

        return type switch
        {
            RaptureHotbarModule.HotbarSlotType.InventoryItem => TryGetInventoryItemSlot(inventory, commandId),
            RaptureHotbarModule.HotbarSlotType.Crystal => inventory->GetInventorySlot(
                InventoryType.Crystals,
                (int)commandId),
            RaptureHotbarModule.HotbarSlotType.KeyItem => inventory->GetInventorySlot(
                InventoryType.KeyItems,
                (int)commandId),
            _ => null,
        };
    }

    public static InventoryItem* ResolveSymbolicInventoryItem(InventoryItem* inventoryItem)
    {
        for (var depth = 0; depth < MaxSymbolicDepth && inventoryItem != null; depth++)
        {
            if (!inventoryItem->IsSymbolic)
                return inventoryItem;

            inventoryItem = inventoryItem->GetLinkedItem();
        }

        return inventoryItem;
    }

    public static bool IsHighQuality(RaptureHotbarModule.HotbarSlotType type, uint commandId)
    {
        if (commandId == 0)
            return false;

        if (type is RaptureHotbarModule.HotbarSlotType.Item or RaptureHotbarModule.HotbarSlotType.EventItem)
            return commandId >= HqItemIdOffset;

        if (type is not (RaptureHotbarModule.HotbarSlotType.InventoryItem
            or RaptureHotbarModule.HotbarSlotType.Crystal
            or RaptureHotbarModule.HotbarSlotType.KeyItem))
            return false;

        var inventoryItem = ResolveSymbolicInventoryItem(TryGetLinkedInventoryItem(type, commandId));
        return inventoryItem != null
            && !inventoryItem->IsEmpty()
            && inventoryItem->IsHighQuality();
    }

    public static int GetInventoryItemCount(uint baseItemId, bool isHq)
    {
        if (baseItemId == 0 || !GameModuleGuard.TryGetInventory(out var inventory))
            return 0;

        return inventory->GetInventoryItemCount(baseItemId, isHq);
    }

    public static bool TryResolveDraggedInventoryItem(
        int inventoryType,
        int slotIndex,
        out RaptureHotbarModule.HotbarSlotType slotType,
        out uint commandId) =>
        TryResolveDraggedInventoryItem(
            DragDropType.Item,
            inventoryType,
            slotIndex,
            referenceIndex: (short)Math.Clamp(slotIndex, short.MinValue, short.MaxValue),
            out slotType,
            out commandId);

    public static bool TryResolveDraggedInventoryItem(
        DragDropType dragType,
        int int1,
        int int2,
        short referenceIndex,
        out RaptureHotbarModule.HotbarSlotType slotType,
        out uint commandId)
    {
        slotType = RaptureHotbarModule.HotbarSlotType.Empty;
        commandId = 0;

        if (!TryResolveDraggedInventoryLocation(dragType, int1, int2, referenceIndex, out var container, out var slot))
            return false;

        return TryResolveInventoryLocation(container, slot, out slotType, out commandId);
    }

    public static bool TryResolveDraggedInventoryLocation(
        DragDropType dragType,
        int int1,
        int int2,
        short referenceIndex,
        out InventoryType container,
        out int slotIndex)
    {
        container = default;
        slotIndex = -1;

        if (!GameModuleGuard.TryGetInventory(out _))
            return false;

        foreach (var candidate in EnumerateInventoryLocationCandidates(dragType, int1, int2, referenceIndex))
        {
            if (!TryInventorySlotHasItem(candidate.Container, candidate.Slot, candidate.ExpectedItemId))
                continue;

            container = candidate.Container;
            slotIndex = candidate.Slot;
            return true;
        }

        return false;
    }

    public static bool TryFindInventorySlotByAppearance(
        ResolvedSlotIcon appearance,
        int? quantity,
        InventoryType? preferredContainer,
        int? preferredSlot,
        out InventoryType container,
        out int slotIndex,
        out uint itemId)
    {
        container = default;
        slotIndex = -1;
        itemId = 0;

        if (appearance.IconId == 0 || !GameModuleGuard.TryGetInventory(out var inventory))
            return false;

        if (preferredContainer is InventoryType hintedContainer
            && preferredSlot is int hintedSlot
            && !InventorySlotHasMatchingAppearance(inventory, hintedContainer, hintedSlot, appearance))
        {
            preferredContainer = null;
            preferredSlot = null;
        }

        var bestScore = int.MinValue;
        InventoryType? bestContainer = null;
        var bestSlot = -1;
        var bestItemId = 0u;

        foreach (var bag in EnumerateDragInventoryContainers())
        {
            for (var slot = 0; slot <= GetMaxSlotIndexForContainer(bag); slot++)
            {
                var inventoryItem = ResolveSymbolicInventoryItem(
                    inventory->GetInventorySlot(bag, slot));
                if (inventoryItem == null || inventoryItem->IsEmpty())
                    continue;

                if (!InventoryItemMatchesAppearance(inventoryItem, appearance))
                    continue;

                var score = ScoreInventorySlotCandidate(
                    (int)bag,
                    slot,
                    preferredContainer,
                    preferredSlot,
                    quantity,
                    inventoryItem);

                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestContainer = bag;
                bestSlot = slot;
                bestItemId = inventoryItem->GetItemId();
            }
        }

        if (bestContainer == null)
            return false;

        container = bestContainer.Value;
        slotIndex = bestSlot;
        itemId = bestItemId;
        return true;
    }

    public static bool TryFindInventorySlotByGameIconId(
        uint gameIconId,
        int? quantity,
        InventoryType? preferredContainer,
        int? preferredSlot,
        short referenceIndex,
        bool? preferHighQuality,
        out InventoryType container,
        out int slotIndex,
        out uint itemId)
    {
        container = default;
        slotIndex = -1;
        itemId = 0;

        if (gameIconId == 0
            || !GameModuleGuard.TryGetHotbar(out var hotbar, out var uiModule)
            || !GameModuleGuard.TryGetInventory(out var inventory))
            return false;

        var preferredSlotIndex = preferredSlot
            ?? (referenceIndex >= 0 && IsPlausibleSlotIndexPublic(referenceIndex) ? referenceIndex : (int?)null);
        InventoryType? bestContainer = null;
        var bestSlot = -1;
        var bestItemId = 0u;
        var bestScore = int.MinValue;

        var found = InventoryDragSafety.TryWithScratchRestore(() =>
        {
            foreach (var bag in EnumerateDragInventoryContainers())
            {
                for (var slot = 0; slot <= GetMaxSlotIndexForContainer(bag); slot++)
                {
                    var inventoryItem = ResolveSymbolicInventoryItem(
                        inventory->GetInventorySlot(bag, slot));
                    if (inventoryItem == null || inventoryItem->IsEmpty())
                        continue;

                    var encoded = EncodeInventoryCommand(bag, slot);
                    int slotIcon;
                    try
                    {
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

                        slotIcon = scratch.GetIconIdForSlot(appearanceType, appearanceId);
                    }
                    catch
                    {
                        continue;
                    }

                    if (slotIcon <= 0 || (uint)slotIcon != gameIconId)
                        continue;

                    var score = ScoreInventorySlotCandidate(
                        (int)bag,
                        slot,
                        preferredContainer,
                        preferredSlotIndex,
                        quantity,
                        inventoryItem);

                    if (preferHighQuality is bool requiredHq)
                        score += inventoryItem->IsHighQuality() == requiredHq ? 50 : -100;

                    if (score < bestScore)
                        continue;

                    bestScore = score;
                    bestContainer = bag;
                    bestSlot = slot;
                    bestItemId = inventoryItem->GetItemId();
                }
            }

            if (bestContainer == null)
                return false;

            return true;
        });

        if (!found || bestContainer == null)
            return false;

        container = bestContainer.Value;
        slotIndex = bestSlot;
        itemId = bestItemId;
        return true;
    }

    public static bool TryResolveInventorySlotByDragIcon(
        ResolvedSlotIcon appearance,
        int? quantity,
        InventoryType? preferredContainer,
        short referenceIndex,
        out InventoryType container,
        out int slotIndex,
        out uint itemId)
    {
        container = default;
        slotIndex = -1;
        itemId = 0;

        if (appearance.IconId == 0)
            return false;

        if (TryFindInventorySlotByGameIconId(
                appearance.IconId,
                quantity,
                preferredContainer,
                preferredSlot: null,
                referenceIndex,
                appearance.IsHighQuality,
                out container,
                out slotIndex,
                out itemId))
            return InventoryDragSafety.TryVerifyDragResolution(appearance, container, slotIndex);

        if (TryFindInventorySlotByAppearance(
                appearance,
                quantity,
                preferredContainer,
                preferredSlot: null,
                out container,
                out slotIndex,
                out itemId)
            && InventoryDragSafety.TryVerifyDragResolution(appearance, container, slotIndex))
            return true;

        if (!appearance.IsHighQuality
            && TryFindInventorySlotByAppearance(
                appearance with { IsHighQuality = true },
                quantity,
                preferredContainer,
                preferredSlot: null,
                out container,
                out slotIndex,
                out itemId)
            && InventoryDragSafety.TryVerifyDragResolution(
                appearance with { IsHighQuality = true },
                container,
                slotIndex))
            return true;

        if (appearance.IsHighQuality
            && TryFindInventorySlotByAppearance(
                appearance with { IsHighQuality = false },
                quantity,
                preferredContainer,
                preferredSlot: null,
                out container,
                out slotIndex,
                out itemId)
            && InventoryDragSafety.TryVerifyDragResolution(
                appearance with { IsHighQuality = false },
                container,
                slotIndex))
            return true;

        return TryFindInventorySlotBySheetIconId(
            appearance.IconId,
            appearance.IsHighQuality,
            quantity,
            preferredContainer,
            referenceIndex,
            out container,
            out slotIndex,
            out itemId)
            && InventoryDragSafety.TryVerifyDragResolution(appearance, container, slotIndex);
    }

    public static bool TryFindInventorySlotBySheetIconId(
        uint sheetIconId,
        bool? preferHighQuality,
        int? quantity,
        InventoryType? preferredContainer,
        short referenceIndex,
        out InventoryType container,
        out int slotIndex,
        out uint itemId)
    {
        container = default;
        slotIndex = -1;
        itemId = 0;

        if (sheetIconId == 0 || !GameModuleGuard.TryGetInventory(out var inventory))
            return false;

        var preferredSlotIndex = referenceIndex >= 0 && IsPlausibleSlotIndexPublic(referenceIndex)
            ? referenceIndex
            : (int?)null;
        var bestScore = int.MinValue;
        InventoryType? bestContainer = null;
        var bestSlot = -1;
        var bestItemId = 0u;

        foreach (var bag in EnumerateDragInventoryContainers())
        {
            for (var slot = 0; slot <= GetMaxSlotIndexForContainer(bag); slot++)
            {
                var inventoryItem = ResolveSymbolicInventoryItem(
                    inventory->GetInventorySlot(bag, slot));
                if (inventoryItem == null || inventoryItem->IsEmpty())
                    continue;

                var resolved = SlotIconResolver.ResolveInventoryItemAppearance(inventoryItem);
                if (resolved.IconId != sheetIconId)
                    continue;

                var score = ScoreInventorySlotCandidate(
                    (int)bag,
                    slot,
                    preferredContainer,
                    preferredSlotIndex,
                    quantity,
                    inventoryItem);

                if (preferHighQuality is bool requiredHq)
                    score += inventoryItem->IsHighQuality() == requiredHq ? 50 : 0;

                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestContainer = bag;
                bestSlot = slot;
                bestItemId = inventoryItem->GetItemId();
            }
        }

        if (bestContainer == null)
            return false;

        container = bestContainer.Value;
        slotIndex = bestSlot;
        itemId = bestItemId;
        return true;
    }

    public static bool TryFindInventorySlotByItemId(
        uint baseItemId,
        InventoryType? preferredContainer,
        int? preferredSlot,
        short referenceIndex,
        out InventoryType container,
        out int slotIndex,
        out uint itemId)
    {
        container = default;
        slotIndex = -1;
        itemId = 0;

        if (baseItemId == 0 || !GameModuleGuard.TryGetInventory(out var inventory))
            return false;

        var bestScore = int.MinValue;
        InventoryType? bestContainer = null;
        var bestSlot = -1;
        var bestItemId = 0u;

        foreach (var bag in EnumerateDragInventoryContainers())
        {
            for (var slot = 0; slot <= GetMaxSlotIndexForContainer(bag); slot++)
            {
                var inventoryItem = ResolveSymbolicInventoryItem(
                    inventory->GetInventorySlot(bag, slot));
                if (inventoryItem == null || inventoryItem->IsEmpty())
                    continue;

                var candidateId = inventoryItem->GetBaseItemId();
                if (candidateId != baseItemId)
                    continue;

                var score = ScoreInventorySlotCandidate(
                    (int)bag,
                    slot,
                    preferredContainer,
                    preferredSlot ?? (referenceIndex >= 0 && IsPlausibleSlotIndexPublic(referenceIndex) ? referenceIndex : null),
                    quantity: null,
                    inventoryItem);

                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestContainer = bag;
                bestSlot = slot;
                bestItemId = inventoryItem->GetItemId();
            }
        }

        if (bestContainer == null)
            return false;

        container = bestContainer.Value;
        slotIndex = bestSlot;
        itemId = bestItemId;
        return true;
    }

    public static bool TryResolveInventoryLocation(
        InventoryType container,
        int slotIndex,
        out RaptureHotbarModule.HotbarSlotType slotType,
        out uint commandId)
    {
        slotType = RaptureHotbarModule.HotbarSlotType.Empty;
        commandId = 0;

        if (!TryInventorySlotHasItem(container, slotIndex, expectedItemId: null, out var itemId))
            return false;

        return TryResolveItemHotbarCommand(itemId, out slotType, out commandId);
    }

    public static bool TryResolveItemHotbarCommand(
        uint itemId,
        out RaptureHotbarModule.HotbarSlotType slotType,
        out uint commandId)
    {
        slotType = RaptureHotbarModule.HotbarSlotType.Empty;
        commandId = 0;

        if (itemId == 0 || !GameModuleGuard.TryGetHotbar(out var hotbar, out var uiModule))
            return false;

        try
        {
            var scratch = hotbar->ScratchSlot;
            scratch.Set(uiModule, RaptureHotbarModule.HotbarSlotType.Item, itemId);

            if (scratch.CommandId == 0
                || scratch.CommandType == RaptureHotbarModule.HotbarSlotType.Empty)
                return false;

            slotType = scratch.CommandType;
            commandId = scratch.CommandId;
            return slotType is RaptureHotbarModule.HotbarSlotType.Item
                or RaptureHotbarModule.HotbarSlotType.EventItem;
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"[EQP] Item hotbar resolve failed (#{itemId}): {ex.Message}");
            return false;
        }
    }

    public static bool TryResolveInventoryLinkedCommand(
        RaptureHotbarModule.HotbarSlotType linkedType,
        uint linkedCommandId,
        out RaptureHotbarModule.HotbarSlotType slotType,
        out uint commandId)
    {
        slotType = RaptureHotbarModule.HotbarSlotType.Empty;
        commandId = 0;

        var inventoryItem = ResolveSymbolicInventoryItem(TryGetLinkedInventoryItem(linkedType, linkedCommandId));
        if (inventoryItem == null || inventoryItem->IsEmpty())
            return false;

        return TryResolveItemHotbarCommand(inventoryItem->GetItemId(), out slotType, out commandId);
    }

    public static uint EncodeInventoryCommand(InventoryType container, int slot) =>
        (uint)(((int)container << 16) | (slot & 0xFFFF));

    private static bool InventoryItemMatchesAppearance(
        InventoryItem* inventoryItem,
        ResolvedSlotIcon appearance)
    {
        var resolved = SlotIconResolver.ResolveInventoryItemAppearance(inventoryItem);
        return resolved.IsValid
            && resolved.IconId == appearance.IconId
            && resolved.IsHighQuality == appearance.IsHighQuality;
    }

    private static bool InventorySlotHasMatchingAppearance(
        InventoryManager* inventory,
        InventoryType container,
        int slotIndex,
        ResolvedSlotIcon appearance)
    {
        if (slotIndex < 0)
            return false;

        var inventoryItem = ResolveSymbolicInventoryItem(inventory->GetInventorySlot(container, slotIndex));
        return inventoryItem != null
            && !inventoryItem->IsEmpty()
            && InventoryItemMatchesAppearance(inventoryItem, appearance);
    }

    private static bool TryInventorySlotHasItem(
        InventoryType container,
        int slotIndex,
        uint? expectedItemId,
        out uint itemId)
    {
        itemId = 0;

        if (!GameModuleGuard.TryGetInventory(out var inventory) || slotIndex < 0)
            return false;

        var inventoryItem = ResolveSymbolicInventoryItem(inventory->GetInventorySlot(container, slotIndex));
        if (inventoryItem == null || inventoryItem->IsEmpty())
            return false;

        itemId = inventoryItem->GetItemId();
        return expectedItemId is not uint requiredItemId || itemId == requiredItemId;
    }

    internal static bool TryInventorySlotHasItem(
        InventoryType container,
        int slotIndex,
        uint? expectedItemId) =>
        TryInventorySlotHasItem(container, slotIndex, expectedItemId, out _);

    private static IEnumerable<InventoryLocationCandidate> EnumerateInventoryLocationCandidates(
        DragDropType dragType,
        int int1,
        int int2,
        short referenceIndex)
    {
        if (dragType == DragDropType.Inventory_Crystal)
        {
            foreach (var slot in EnumeratePlausibleSlotIndices(int2, referenceIndex))
                yield return new InventoryLocationCandidate(InventoryType.Crystals, slot, null);
            yield break;
        }

        foreach (var candidate in EnumerateMainBagCandidates(int1, int2, referenceIndex))
            yield return candidate;

        if (LooksLikeDirectItemId(int2))
        {
            foreach (var candidate in EnumerateItemIdMatchedCandidates(int2, referenceIndex))
                yield return candidate;
        }

        if (LooksLikeDirectItemId(int1))
        {
            foreach (var candidate in EnumerateItemIdMatchedCandidates(int1, referenceIndex))
                yield return candidate;
        }

        if (IsPlausibleSlotIndex(int2))
            yield return new InventoryLocationCandidate(InventoryType.Crystals, int2, null);

        if (IsPlausibleSlotIndex(int2))
            yield return new InventoryLocationCandidate(InventoryType.KeyItems, int2, null);

        if (referenceIndex >= 0 && IsPlausibleSlotIndex(referenceIndex))
            yield return new InventoryLocationCandidate(InventoryType.KeyItems, referenceIndex, null);
    }

    private static IEnumerable<InventoryLocationCandidate> EnumerateMainBagCandidates(
        int int1,
        int int2,
        short referenceIndex)
    {
        foreach (var candidate in EnumerateTypedBagCandidates(int1, int2, referenceIndex, IsMainInventoryType, useContainerHint: false))
            yield return candidate;

        foreach (var candidate in EnumerateTypedBagCandidates(int1, int2, referenceIndex, IsSaddleBagInventoryType, useContainerHint: false))
            yield return candidate;

        foreach (var candidate in EnumerateTypedBagCandidates(int1, int2, referenceIndex, IsArmoryInventoryType, useContainerHint: true))
            yield return candidate;

        foreach (var candidate in EnumerateTypedBagCandidates(int1, int2, referenceIndex, IsRetainerPageInventoryType, useContainerHint: true))
            yield return candidate;
    }

    private static IEnumerable<InventoryLocationCandidate> EnumerateTypedBagCandidates(
        int int1,
        int int2,
        short referenceIndex,
        Func<int, bool> isInventoryType,
        bool useContainerHint)
    {
        if (isInventoryType(int1) && HasPlausibleSlotHint(int1, int2, referenceIndex))
        {
            var container = (InventoryType)int1;
            foreach (var slot in EnumeratePlausibleSlotIndices(
                         int2,
                         referenceIndex,
                         useContainerHint ? container : null))
                yield return new InventoryLocationCandidate(container, slot, null);
        }

        if (isInventoryType(int2) && HasPlausibleSlotHint(int2, int1, referenceIndex))
        {
            var container = (InventoryType)int2;
            foreach (var slot in EnumeratePlausibleSlotIndices(
                         int1,
                         referenceIndex,
                         useContainerHint ? container : null))
                yield return new InventoryLocationCandidate(container, slot, null);
        }
    }

    private static IEnumerable<InventoryLocationCandidate> EnumerateItemIdMatchedCandidates(
        int expectedItemId,
        short referenceIndex)
    {
        if (referenceIndex >= 0 && IsPlausibleSlotIndex(referenceIndex))
        {
            foreach (var bag in EnumerateDragInventoryContainers())
            {
                yield return new InventoryLocationCandidate(
                    bag,
                    referenceIndex,
                    (uint)expectedItemId);
            }
        }
    }

    private static IEnumerable<int> EnumeratePlausibleSlotIndices(
        int primarySlot,
        short referenceIndex,
        InventoryType? containerHint = null)
    {
        var seen = new HashSet<int>();

        if (IsPlausibleSlotIndex(primarySlot, containerHint) && seen.Add(primarySlot))
            yield return primarySlot;

        if (referenceIndex >= 0
            && IsPlausibleSlotIndex(referenceIndex, containerHint)
            && seen.Add(referenceIndex))
            yield return referenceIndex;
    }

    private static bool HasPlausibleSlotHint(int inventoryType, int otherInt, short referenceIndex) =>
        inventoryType != 0
        || IsPlausibleSlotIndex(otherInt)
        || (referenceIndex >= 0 && IsPlausibleSlotIndex(referenceIndex));

    internal static bool IsMainInventoryType(int value) =>
        value is >= (int)InventoryType.Inventory1 and <= (int)InventoryType.Inventory4;

    internal static bool IsSaddleBagInventoryType(int value) =>
        value is (int)InventoryType.SaddleBag1
            or (int)InventoryType.SaddleBag2
            or (int)InventoryType.PremiumSaddleBag1
            or (int)InventoryType.PremiumSaddleBag2;

    internal static bool IsArmoryInventoryType(int value) =>
        value is (int)InventoryType.ArmoryOffHand
            or (int)InventoryType.ArmoryHead
            or (int)InventoryType.ArmoryBody
            or (int)InventoryType.ArmoryHands
            or (int)InventoryType.ArmoryLegs
            or (int)InventoryType.ArmoryFeets
            or (int)InventoryType.ArmoryEar
            or (int)InventoryType.ArmoryNeck
            or (int)InventoryType.ArmoryWrist
            or (int)InventoryType.ArmoryRings
            or (int)InventoryType.ArmorySoulCrystal
            or (int)InventoryType.ArmoryMainHand;

    internal static bool IsRetainerPageInventoryType(int value) =>
        value is >= (int)InventoryType.RetainerPage1 and <= (int)InventoryType.RetainerPage7;

    internal static bool IsDragResolvableInventoryType(int value) =>
        IsMainInventoryType(value)
        || IsSaddleBagInventoryType(value)
        || IsArmoryInventoryType(value)
        || IsRetainerPageInventoryType(value);

    internal static int GetMaxSlotIndexForContainer(InventoryType container)
    {
        if (GameModuleGuard.TryGetInventory(out var inventory))
        {
            try
            {
                var invContainer = inventory->GetInventoryContainer(container);
                if (invContainer != null)
                {
                    var size = invContainer->GetSize();
                    if (size > 0)
                        return size - 1;
                }
            }
            catch (Exception ex)
            {
                PluginLog.Debug($"[EQP] Inventory container size lookup failed ({container}): {ex.Message}");
            }
        }

        if (container == InventoryType.ArmoryMainHand)
            return MaxArmoryMainHandSlotIndex;

        if (IsRetainerPageInventoryType((int)container))
            return MaxRetainerPageSlotIndex;

        return MaxInventoryGridSlotIndex;
    }

    internal static bool IsPlausibleSlotIndexForContainer(InventoryType container, int value) =>
        value is >= 0 and var slot && slot <= GetMaxSlotIndexForContainer(container);

    internal static IEnumerable<InventoryType> EnumerateDragInventoryContainers()
    {
        yield return InventoryType.Inventory1;
        yield return InventoryType.Inventory2;
        yield return InventoryType.Inventory3;
        yield return InventoryType.Inventory4;
        yield return InventoryType.SaddleBag1;
        yield return InventoryType.SaddleBag2;
        yield return InventoryType.PremiumSaddleBag1;
        yield return InventoryType.PremiumSaddleBag2;
        yield return InventoryType.ArmoryMainHand;
        yield return InventoryType.ArmoryOffHand;
        yield return InventoryType.ArmoryHead;
        yield return InventoryType.ArmoryBody;
        yield return InventoryType.ArmoryHands;
        yield return InventoryType.ArmoryLegs;
        yield return InventoryType.ArmoryFeets;
        yield return InventoryType.ArmoryEar;
        yield return InventoryType.ArmoryNeck;
        yield return InventoryType.ArmoryWrist;
        yield return InventoryType.ArmoryRings;
        yield return InventoryType.ArmorySoulCrystal;
        yield return InventoryType.RetainerPage1;
        yield return InventoryType.RetainerPage2;
        yield return InventoryType.RetainerPage3;
        yield return InventoryType.RetainerPage4;
        yield return InventoryType.RetainerPage5;
        yield return InventoryType.RetainerPage6;
        yield return InventoryType.RetainerPage7;
    }

    private static int ScoreInventorySlotCandidate(
        int bag,
        int slot,
        InventoryType? preferredContainer,
        int? preferredSlot,
        int? quantity,
        InventoryItem* inventoryItem)
    {
        var score = 0;
        if (preferredContainer == (InventoryType)bag && preferredSlot == slot)
            score += 100;
        else if (preferredContainer == (InventoryType)bag)
            score += 20;
        else if (preferredSlot == slot)
            score += 5;

        if (quantity is int requiredQuantity
            && inventoryItem->GetQuantity() == (uint)requiredQuantity)
            score += 10;

        return score;
    }

    internal static bool IsPlausibleSlotIndexPublic(int value) =>
        value is >= 0 and <= MaxInventoryGridSlotIndex;

    internal static short ResolveInventorySlotHint(
        short referenceIndex,
        int? uiGridSlot,
        InventoryType? containerHint = null)
    {
        var maxSlot = containerHint is InventoryType container
            ? GetMaxSlotIndexForContainer(container)
            : MaxInventoryGridSlotIndex;

        if (uiGridSlot is int gridSlot && gridSlot >= 0 && gridSlot <= maxSlot)
            return (short)gridSlot;

        if (referenceIndex >= 0 && referenceIndex <= maxSlot)
            return referenceIndex;

        return -1;
    }

    private static bool IsPlausibleSlotIndex(int value, InventoryType? containerHint = null) =>
        containerHint is InventoryType container
            ? IsPlausibleSlotIndexForContainer(container, value)
            : IsPlausibleSlotIndexPublic(value);

    internal static bool LooksLikeDirectItemId(int value) =>
        value > MaxInventoryGridSlotIndex;

    private static InventoryItem* TryGetInventoryItemSlot(InventoryManager* inventory, uint commandId)
    {
        var containerHigh = (InventoryType)(commandId >> 16);
        var slotLow = (int)(commandId & 0xFFFF);
        var containerLow = (InventoryType)(commandId & 0xFFFF);
        var slotHigh = (int)(commandId >> 16);

        InventoryItem*[] candidates =
        [
            inventory->GetInventorySlot(containerHigh, slotLow),
            inventory->GetInventorySlot(containerLow, slotHigh),
        ];

        foreach (var candidate in candidates)
        {
            if (candidate != null && !candidate->IsEmpty())
                return candidate;
        }

        return null;
    }

    /// <summary>A candidate inventory location for an item.</summary>
    private readonly record struct InventoryLocationCandidate(
        InventoryType Container,
        int Slot,
        uint? ExpectedItemId);
}

