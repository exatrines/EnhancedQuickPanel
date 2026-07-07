using EnhancedQuickPanel.Models;
using EnhancedQuickPanel.Services.CustomIcons;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using GameAction = Lumina.Excel.Sheets.Action;

namespace EnhancedQuickPanel.Services;

/// <summary>A resolved icon ID and whether it is the high-quality variant.</summary>
internal readonly record struct ResolvedSlotIcon(uint IconId, bool IsHighQuality)
{
    internal const uint MaxResolvableIconId = 2_000_000;

    public static ResolvedSlotIcon Empty => default;

    public bool IsValid =>
        IconId is > 0 and <= MaxResolvableIconId
        || CustomIconIds.IsCustom(IconId);
}

/// <summary>Determines the icon ID and tooltip for a slot's content.</summary>
internal static unsafe class SlotIconResolver
{
    private const uint MaxLoadableIconId = ResolvedSlotIcon.MaxResolvableIconId;

    private static readonly Dictionary<(byte Type, uint CommandId), ResolvedSlotIcon> HotbarIconCache = [];

    public static void ClearCache() => HotbarIconCache.Clear();

    public static ResolvedSlotIcon ResolveNativeIcon(int slotIndex, RaptureHotbarModule.HotbarSlotType type, uint commandId)
    {
        if (NativeQuickPanelUiReader.TryGetSlotIcon(slotIndex, out var uiIcon) && uiIcon.IsValid)
            return ApplyHighQuality(uiIcon, type, commandId);

        return ResolveHotbarIcon(type, commandId);
    }

    public static ResolvedSlotIcon ResolveNativeIcon(RaptureHotbarModule.HotbarSlotType type, uint commandId) =>
        ResolveHotbarIcon(type, commandId);

    public static uint ResolveNativeIconId(RaptureHotbarModule.HotbarSlotType type, uint commandId) =>
        ResolveHotbarIcon(type, commandId).IconId;

    public static string ResolveNativeTooltip(RaptureHotbarModule.HotbarSlotType type, uint commandId) =>
        SlotTooltipResolver.ResolveNative(type, commandId);

    public static string ResolveTooltip(PanelSlot slot) =>
        SlotTooltipResolver.Resolve(slot);

    public static ResolvedSlotIcon ResolveIcon(PanelSlot slot)
    {
        if (!GameModuleGuard.IsClientReady)
            return ResolvedSlotIcon.Empty;

        if (slot.IconId != 0 && IsLoadableIcon(slot.IconId))
            return new ResolvedSlotIcon(slot.IconId, false);

        return slot.Kind switch
        {
            PanelSlotKind.Action => ResolveHotbarIcon((RaptureHotbarModule.HotbarSlotType)slot.CommandType, slot.CommandId),
            PanelSlotKind.Macro => ResolveMacroIcon(slot),
            PanelSlotKind.TextCommand => ResolvedSlotIcon.Empty,
            _ => ResolvedSlotIcon.Empty,
        };
    }

    public static uint ResolveIconId(PanelSlot slot) => ResolveIcon(slot).IconId;

    public static uint EncodeMacroCommandId(PanelSlot slot) =>
        MacroSlotResolver.EncodeHotbarCommandId(slot.MacroSet, slot.MacroIndex);

    public static uint EncodeMacroCommandId(byte macroSet, byte macroIndex) =>
        MacroSlotResolver.EncodeHotbarCommandId(macroSet, macroIndex);

    public static (byte MacroSet, byte MacroIndex) DecodeMacroCommandId(uint commandId) =>
        MacroSlotResolver.DecodeHotbarCommandId(commandId);

    public static (byte MacroSet, byte MacroIndex) DecodeQuickPanelMacroCommandId(uint commandId) =>
        MacroSlotResolver.DecodeQuickPanelMacroCommandId(commandId);

    private static ResolvedSlotIcon ResolveHotbarIcon(RaptureHotbarModule.HotbarSlotType type, uint commandId)
    {
        if (type == RaptureHotbarModule.HotbarSlotType.Empty || commandId == 0)
            return ResolvedSlotIcon.Empty;

        var cacheKey = ((byte)type, commandId);
        if (ShouldCacheIcon(type) && HotbarIconCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var icon = ResolveHotbarIconUncached(type, commandId);
        if (icon.IsValid && ShouldCacheIcon(type))
            HotbarIconCache[cacheKey] = icon;

        return icon;
    }

    private static bool ShouldCacheIcon(RaptureHotbarModule.HotbarSlotType type) =>
        type != RaptureHotbarModule.HotbarSlotType.Macro;

    private static ResolvedSlotIcon ResolveHotbarIconUncached(RaptureHotbarModule.HotbarSlotType type, uint commandId)
    {
        if (type == RaptureHotbarModule.HotbarSlotType.Macro)
            return ResolveMacroIconFromCommandId(commandId);

        if (InventorySlotHelper.IsItemSlotType(type))
        {
            var inventoryIcon = type is RaptureHotbarModule.HotbarSlotType.InventoryItem
                or RaptureHotbarModule.HotbarSlotType.Crystal
                or RaptureHotbarModule.HotbarSlotType.KeyItem
                ? ResolveInventoryLinkedIcon(type, commandId)
                : ResolveInventoryLinkedIconFromItemCommand(commandId);

            if (inventoryIcon.IsValid)
                return inventoryIcon;
        }

        var isHq = InventorySlotHelper.IsHighQuality(type, commandId);
        if (!GameModuleGuard.TryGetHotbar(out var hotbar, out var uiModule))
            return LuminaIconFallback(type, commandId, isHq);

        try
        {
            var scratch = hotbar->ScratchSlot;
            scratch.Set(uiModule, type, commandId);
            scratch.LoadIconId();
            scratch.LoadCostDataForSlot();

            RaptureHotbarModule.HotbarSlotType appearanceType = default;
            uint appearanceId = 0;
            ushort appearanceData = 0;
            RaptureHotbarModule.GetSlotAppearance(&appearanceType, &appearanceId, &appearanceData, hotbar, &scratch);

            if (appearanceType == RaptureHotbarModule.HotbarSlotType.Empty || appearanceId == 0)
            {
                appearanceType = GetAppearanceType(scratch, type);
                appearanceId = GetAppearanceId(scratch, commandId);
            }

            isHq |= InventorySlotHelper.IsHighQuality(appearanceType, appearanceId);

            var icon = scratch.GetIconIdForSlot(appearanceType, appearanceId);
            if (!IsLoadableIcon((uint)icon) && IsItemLikeType(appearanceType))
            {
                var (baseItemId, _) = InventorySlotHelper.DecodeItemId(appearanceId);
                icon = scratch.GetIconIdForSlot(appearanceType, baseItemId);
            }

            if (IsLoadableIcon((uint)icon))
                return new ResolvedSlotIcon((uint)icon, isHq);
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"[EQP] Hotbar icon read failed ({type} #{commandId}): {ex.Message}");
        }

        return LuminaIconFallback(type, commandId, isHq);
    }

    private static ResolvedSlotIcon ResolveInventoryLinkedIconFromItemCommand(uint commandId)
    {
        var (baseItemId, isHq) = InventorySlotHelper.DecodeItemId(commandId);
        if (baseItemId == 0)
            return ResolvedSlotIcon.Empty;

        var row = Svc.Data.GetExcelSheet<Item>()?.GetRowOrDefault(baseItemId);
        if (row == null)
            return ResolvedSlotIcon.Empty;

        var iconId = (uint)row.Value.Icon;
        return IsLoadableIcon(iconId) ? new ResolvedSlotIcon(iconId, isHq) : ResolvedSlotIcon.Empty;
    }

    private static ResolvedSlotIcon ApplyHighQuality(
        ResolvedSlotIcon icon,
        RaptureHotbarModule.HotbarSlotType type,
        uint commandId)
    {
        if (!InventorySlotHelper.IsHighQuality(type, commandId))
            return icon;

        return icon.IsHighQuality ? icon : icon with { IsHighQuality = true };
    }

    private static bool IsItemLikeType(RaptureHotbarModule.HotbarSlotType type) =>
        type is RaptureHotbarModule.HotbarSlotType.Item
            or RaptureHotbarModule.HotbarSlotType.EventItem
            or RaptureHotbarModule.HotbarSlotType.InventoryItem;

    private static ResolvedSlotIcon ResolveInventoryLinkedIcon(RaptureHotbarModule.HotbarSlotType type, uint commandId) =>
        ResolveInventoryItemIcon(InventorySlotHelper.TryGetLinkedInventoryItem(type, commandId));

    internal static ResolvedSlotIcon ResolveInventoryItemAppearance(InventoryItem* inventoryItem) =>
        ResolveInventoryItemIcon(inventoryItem);

    private static ResolvedSlotIcon ResolveInventoryItemIcon(InventoryItem* inventoryItem)
    {
        if (inventoryItem == null)
            return ResolvedSlotIcon.Empty;

        inventoryItem = InventorySlotHelper.ResolveSymbolicInventoryItem(inventoryItem);
        if (inventoryItem == null || inventoryItem->IsEmpty())
            return ResolvedSlotIcon.Empty;

        var itemId = inventoryItem->GetBaseItemId();
        if (itemId == 0)
            return ResolvedSlotIcon.Empty;

        var row = Svc.Data.GetExcelSheet<Item>()?.GetRowOrDefault(itemId);
        if (row != null)
        {
            var isHq = inventoryItem->IsHighQuality();
            var iconId = (uint)row.Value.Icon;
            return IsLoadableIcon(iconId) ? new ResolvedSlotIcon(iconId, isHq) : ResolvedSlotIcon.Empty;
        }

        var eventRow = Svc.Data.GetExcelSheet<EventItem>()?.GetRowOrDefault(itemId);
        if (eventRow == null)
            return ResolvedSlotIcon.Empty;

        var eventIconId = (uint)eventRow.Value.Icon;
        return IsLoadableIcon(eventIconId) ? new ResolvedSlotIcon(eventIconId, false) : ResolvedSlotIcon.Empty;
    }

    private static ResolvedSlotIcon ResolveMacroIcon(PanelSlot slot) =>
        ResolveMacroIconFromIndices(slot.MacroSet, slot.MacroIndex);

    private static ResolvedSlotIcon ResolveMacroIconFromCommandId(uint commandId)
    {
        if (commandId == 0)
            return ResolvedSlotIcon.Empty;

        if (!MacroSlotResolver.TryResolveMacroSlot(commandId, out var macroSet, out var macroIndex))
            return ResolvedSlotIcon.Empty;

        return ResolveMacroIconFromIndices(macroSet, macroIndex);
    }

    private static ResolvedSlotIcon ResolveMacroIconFromIndices(byte macroSet, byte macroIndex)
    {
        if (!GameModuleGuard.TryGetMacroModule(out var macroModule) || !GameModuleGuard.TryGetHotbar(out var hotbar, out var uiModule))
            return ResolvedSlotIcon.Empty;

        try
        {
            var macro = macroModule->GetMacro(macroSet, macroIndex);

            if (MacroIconResolver.TryResolveFromMacro(macro, out var miconIconId)
                && IsLoadableIcon(miconIconId))
            {
                return new ResolvedSlotIcon(miconIconId, false);
            }

            var macroCommandId = EncodeMacroCommandId(macroSet, macroIndex);

            RaptureHotbarModule.HotbarSlotType type = default;
            uint rowId = 0;
            uint itemId = 0;
            if (macroModule->TryResolveMacroIcon(uiModule, &type, &rowId, macroSet, macroIndex, &itemId))
            {
                if (IsLoadableIcon(itemId))
                    return new ResolvedSlotIcon(itemId, InventorySlotHelper.IsHighQuality(type, itemId));

                var luminaIcon = LuminaIconFallback(type, rowId);
                if (luminaIcon.IsValid)
                    return luminaIcon;

                if (type != RaptureHotbarModule.HotbarSlotType.Empty && rowId != 0)
                {
                    var scratch = hotbar->ScratchSlot;
                    scratch.Set(uiModule, type, rowId);
                    scratch.LoadIconId();
                    var resolvedIcon = scratch.GetIconIdForSlot(type, rowId);
                    if (IsLoadableIcon((uint)resolvedIcon))
                        return new ResolvedSlotIcon((uint)resolvedIcon, InventorySlotHelper.IsHighQuality(type, rowId));
                }
            }

            var storedIcon = ResolveStoredMacroIcon(macro);
            if (storedIcon.IsValid)
                return storedIcon;

            var fallbackScratch = hotbar->ScratchSlot;
            fallbackScratch.Set(uiModule, RaptureHotbarModule.HotbarSlotType.Macro, macroCommandId);
            fallbackScratch.LoadIconId();

            var icon = fallbackScratch.GetIconIdForSlot(RaptureHotbarModule.HotbarSlotType.Macro, macroCommandId);
            return IsLoadableIcon((uint)icon)
                ? new ResolvedSlotIcon((uint)icon, false)
                : ResolvedSlotIcon.Empty;
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"[EQP] Macro icon read failed (set={macroSet} index={macroIndex}): {ex.Message}");
            return ResolvedSlotIcon.Empty;
        }
    }

    private static ResolvedSlotIcon ResolveStoredMacroIcon(RaptureMacroModule.Macro* macro)
    {
        if (macro == null)
            return ResolvedSlotIcon.Empty;

        if (IsLoadableIcon(macro->IconId))
            return new ResolvedSlotIcon(macro->IconId, false);

        if (macro->MacroIconRowId == 0)
            return ResolvedSlotIcon.Empty;

        foreach (var rowId in new[] { macro->MacroIconRowId, macro->MacroIconRowId - 1 })
        {
            if (rowId == 0)
                continue;

            var markerIcon = LuminaIconFallback(RaptureHotbarModule.HotbarSlotType.Marker, rowId);
            if (markerIcon.IsValid)
                return markerIcon;

            var fieldMarkerIcon = LuminaIconFallback(RaptureHotbarModule.HotbarSlotType.FieldMarker, rowId);
            if (fieldMarkerIcon.IsValid)
                return fieldMarkerIcon;
        }

        return ResolvedSlotIcon.Empty;
    }

    private static RaptureHotbarModule.HotbarSlotType GetAppearanceType(
        RaptureHotbarModule.HotbarSlot scratch,
        RaptureHotbarModule.HotbarSlotType fallback) =>
        scratch.ApparentSlotType != RaptureHotbarModule.HotbarSlotType.Empty
            ? scratch.ApparentSlotType
            : scratch.CommandType != RaptureHotbarModule.HotbarSlotType.Empty
                ? scratch.CommandType
                : fallback;

    private static uint GetAppearanceId(RaptureHotbarModule.HotbarSlot scratch, uint fallback) =>
        scratch.ApparentActionId != 0
            ? scratch.ApparentActionId
            : scratch.CommandId != 0
                ? scratch.CommandId
                : fallback;

    private static ResolvedSlotIcon LuminaIconFallback(
        RaptureHotbarModule.HotbarSlotType type,
        uint commandId,
        bool isHq = false)
    {
        if (commandId == 0)
            return ResolvedSlotIcon.Empty;

        try
        {
            uint iconId;
            if (type is RaptureHotbarModule.HotbarSlotType.Item or RaptureHotbarModule.HotbarSlotType.EventItem)
            {
                iconId = GetItemRowIcon(commandId, out var decodedHq);
                isHq |= decodedHq;
            }
            else
            {
                iconId = type switch
                {
                    RaptureHotbarModule.HotbarSlotType.Action =>
                        GetRowIcon<GameAction>(commandId, row => (uint)row.Icon),
                    RaptureHotbarModule.HotbarSlotType.CraftAction =>
                        GetRowIcon<CraftAction>(commandId, row => (uint)row.Icon),
                    RaptureHotbarModule.HotbarSlotType.GeneralAction =>
                        GetRowIcon<GeneralAction>(commandId, row => (uint)row.Icon),
                    RaptureHotbarModule.HotbarSlotType.Emote =>
                        GetRowIcon<Emote>(commandId, row => (uint)row.Icon),
                    RaptureHotbarModule.HotbarSlotType.Mount =>
                        GetRowIcon<Mount>(commandId, row => (uint)row.Icon),
                    RaptureHotbarModule.HotbarSlotType.BuddyAction =>
                        GetRowIcon<BuddyAction>(commandId, row => (uint)row.Icon),
                    RaptureHotbarModule.HotbarSlotType.PetAction =>
                        GetRowIcon<PetAction>(commandId, row => (uint)row.Icon),
                    RaptureHotbarModule.HotbarSlotType.MainCommand =>
                        GetRowIcon<MainCommand>(commandId, row => (uint)row.Icon),
                    RaptureHotbarModule.HotbarSlotType.Marker =>
                        GetRowIcon<Marker>(commandId, row => (uint)row.Icon),
                    RaptureHotbarModule.HotbarSlotType.FieldMarker =>
                        GetRowIcon<Lumina.Excel.Sheets.FieldMarker>(commandId, row => (uint)row.UiIcon),
                    _ => 0u,
                };
            }

            return iconId != 0 ? new ResolvedSlotIcon(iconId, isHq) : ResolvedSlotIcon.Empty;
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"[EQP] Lumina icon fallback failed ({type} #{commandId}): {ex.Message}");
            return ResolvedSlotIcon.Empty;
        }
    }

    private static uint GetItemRowIcon(uint commandId, out bool isHq)
    {
        var (baseItemId, decodedHq) = InventorySlotHelper.DecodeItemId(commandId);
        isHq = decodedHq;
        return GetRowIcon<Item>(baseItemId, row => (uint)row.Icon);
    }

    private static uint GetRowIcon<T>(uint commandId, Func<T, uint> getIcon) where T : struct, Lumina.Excel.IExcelRow<T>
    {
        var row = Svc.Data.GetExcelSheet<T>()?.GetRowOrDefault(commandId);
        if (row == null)
            return 0;

        var icon = getIcon(row.Value);
        return IsLoadableIcon(icon) ? icon : 0;
    }

    private static bool IsLoadableIcon(uint iconId) =>
        iconId is > 0 and <= MaxLoadableIconId || CustomIconIds.IsCustom(iconId);
}

