using EnhancedQuickPanel.Models;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using GameAction = Lumina.Excel.Sheets.Action;

namespace EnhancedQuickPanel.Services;

/// <summary>Builds the tooltip text shown when hovering a slot.</summary>
internal static unsafe class SlotTooltipResolver
{
    public static string Resolve(PanelSlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.Label))
            return slot.Label.Trim();

        return slot.Kind switch
        {
            PanelSlotKind.Action => ResolveActionTooltip(
                (RaptureHotbarModule.HotbarSlotType)slot.CommandType,
                slot.CommandId),
            PanelSlotKind.Macro => ResolveMacroTooltip(slot),
            PanelSlotKind.TextCommand => ResolveTextCommandTooltip(slot),
            _ => string.Empty,
        };
    }

    public static string ResolveNative(
        RaptureHotbarModule.HotbarSlotType type,
        uint commandId)
    {
        if (type == RaptureHotbarModule.HotbarSlotType.Empty || commandId == 0)
            return string.Empty;

        var fallback = FormatFallback(type, commandId);
        if (!ScratchSlotHelper.TryConfigure(type, commandId, scratch =>
            {
                scratch->LoadIconId();
                scratch->LoadCostDataForSlot();
            }))
            return fallback;

        if (!GameModuleGuard.TryGetHotbar(out var hotbar, out _))
            return fallback;

        try
        {
            var scratch = &hotbar->ScratchSlot;
            RaptureHotbarModule.HotbarSlotType appearanceType = default;
            uint appearanceId = 0;
            ushort appearanceData = 0;
            RaptureHotbarModule.GetSlotAppearance(
                &appearanceType,
                &appearanceId,
                &appearanceData,
                hotbar,
                scratch);

            if (appearanceType == RaptureHotbarModule.HotbarSlotType.Empty || appearanceId == 0)
            {
                appearanceType = GetAppearanceType(*scratch, type);
                appearanceId = GetAppearanceId(*scratch, commandId);
            }

            var name = SeStringTextHelper.ReadPlainText(
                scratch->GetDisplayNameForSlot(appearanceType, appearanceId));
            if (SeStringTextHelper.LooksLikeUnparsedSeString(name))
                name = string.Empty;

            return string.IsNullOrWhiteSpace(name) ? fallback : name;
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"[EQP] Tooltip read failed ({type} #{commandId}): {ex.Message}");
            return fallback;
        }
    }

    private static string ResolveActionTooltip(
        RaptureHotbarModule.HotbarSlotType type,
        uint commandId)
    {
        if (commandId == 0)
            return string.Empty;

        var native = ResolveNative(type, commandId);
        if (!IsFallbackLabel(native, type, commandId)
            && !SeStringTextHelper.LooksLikeUnparsedSeString(native))
            return native;

        var lumina = ResolveLuminaTooltip(type, commandId);
        return string.IsNullOrWhiteSpace(lumina) ? native : lumina;
    }

    private static string ResolveMacroTooltip(PanelSlot slot)
    {
        if (GameModuleGuard.TryGetMacroModule(out var macroModule))
        {
            try
            {
                var macro = macroModule->GetMacro(slot.MacroSet, slot.MacroIndex);
                if (macro != null)
                {
                    var macroName = SeStringTextHelper.ReadPlainText(macro->Name);
                    if (!string.IsNullOrWhiteSpace(macroName))
                        return macroName;
                }
            }
            catch (Exception ex)
            {
                PluginLog.Debug($"[EQP] Macro tooltip read failed (set={slot.MacroSet} index={slot.MacroIndex}): {ex.Message}");
            }
        }

        var commandId = slot.CommandId != 0
            ? slot.CommandId
            : MacroSlotResolver.EncodeQuickPanelMacroCommandId(slot.MacroSet, slot.MacroIndex);

        var native = ResolveNative(RaptureHotbarModule.HotbarSlotType.Macro, commandId);
        if (!IsFallbackLabel(native, RaptureHotbarModule.HotbarSlotType.Macro, commandId)
            && !SeStringTextHelper.LooksLikeUnparsedSeString(native))
            return native;

        var setLabel = slot.MacroSet == 0 ? T("macroContent.referencePersonal") : T("macroContent.referenceShared");
        return T("macroContent.macroTooltip", setLabel, slot.MacroIndex);
    }

    private static string ResolveTextCommandTooltip(PanelSlot slot)
    {
        var firstLine = slot.TextBody
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(firstLine) ? T("slot.tooltip.customCommand") : firstLine;
    }

    private static string ResolveLuminaTooltip(
        RaptureHotbarModule.HotbarSlotType type,
        uint commandId)
    {
        if (commandId == 0)
            return string.Empty;

        try
        {
            return type switch
            {
                RaptureHotbarModule.HotbarSlotType.Item
                    or RaptureHotbarModule.HotbarSlotType.EventItem =>
                    ResolveItemTooltip(commandId),
                RaptureHotbarModule.HotbarSlotType.InventoryItem
                    or RaptureHotbarModule.HotbarSlotType.Crystal
                    or RaptureHotbarModule.HotbarSlotType.KeyItem =>
                    ResolveInventoryLinkedTooltip(type, commandId),
                RaptureHotbarModule.HotbarSlotType.Action =>
                    GetSheetName<GameAction>(commandId, row => row.Name.ToString()),
                RaptureHotbarModule.HotbarSlotType.CraftAction =>
                    GetSheetName<CraftAction>(commandId, row => row.Name.ToString()),
                RaptureHotbarModule.HotbarSlotType.GeneralAction =>
                    GetSheetName<GeneralAction>(commandId, row => row.Name.ToString()),
                RaptureHotbarModule.HotbarSlotType.Emote =>
                    GetSheetName<Emote>(commandId, row => row.Name.ToString()),
                RaptureHotbarModule.HotbarSlotType.Mount =>
                    GetSheetName<Mount>(commandId, row => row.Singular.ToString()),
                RaptureHotbarModule.HotbarSlotType.MainCommand =>
                    GetSheetName<MainCommand>(commandId, row => row.Name.ToString()),
                RaptureHotbarModule.HotbarSlotType.Marker =>
                    GetSheetName<Marker>(commandId, row => row.Name.ToString()),
                RaptureHotbarModule.HotbarSlotType.FieldMarker =>
                    GetSheetName<FieldMarker>(commandId, row => row.Name.ToString()),
                RaptureHotbarModule.HotbarSlotType.BuddyAction =>
                    GetSheetName<BuddyAction>(commandId, row => row.Name.ToString()),
                RaptureHotbarModule.HotbarSlotType.PetAction =>
                    GetSheetName<PetAction>(commandId, row => row.Name.ToString()),
                RaptureHotbarModule.HotbarSlotType.Companion =>
                    GetSheetName<Companion>(commandId, row => row.Singular.ToString()),
                RaptureHotbarModule.HotbarSlotType.GearSet =>
                    ResolveGearSetTooltip(commandId),
                _ => string.Empty,
            };
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"[EQP] Lumina tooltip fallback failed ({type} #{commandId}): {ex.Message}");
            return string.Empty;
        }
    }

    private static string ResolveInventoryLinkedTooltip(
        RaptureHotbarModule.HotbarSlotType type,
        uint commandId)
    {
        var inventoryItem = InventorySlotHelper.TryGetLinkedInventoryItem(type, commandId);
        if (inventoryItem == null || inventoryItem->IsEmpty())
            return string.Empty;

        inventoryItem = InventorySlotHelper.ResolveSymbolicInventoryItem(inventoryItem);
        if (inventoryItem == null || inventoryItem->IsEmpty())
            return string.Empty;

        var itemId = inventoryItem->GetBaseItemId();
        if (itemId == 0)
            return string.Empty;

        var name = ResolveItemTooltip(itemId);
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        return inventoryItem->IsHighQuality() && !name.EndsWith(" HQ", StringComparison.Ordinal)
            ? $"{name} HQ"
            : name;
    }

    private static string ResolveItemTooltip(uint commandId)
    {
        var (baseItemId, isHq) = InventorySlotHelper.DecodeItemId(commandId);
        if (baseItemId == 0)
            return string.Empty;

        var itemName = GetSheetName<Item>(baseItemId, row => row.Name.ToString());
        if (!string.IsNullOrWhiteSpace(itemName))
            return isHq ? $"{itemName} HQ" : itemName;

        return GetSheetName<EventItem>(baseItemId, row => row.Name.ToString());
    }

    private static string ResolveGearSetTooltip(uint commandId)
    {
        var gearsetName = ResolveNative(RaptureHotbarModule.HotbarSlotType.GearSet, commandId);
        if (!IsFallbackLabel(gearsetName, RaptureHotbarModule.HotbarSlotType.GearSet, commandId))
            return gearsetName;

        return T("slot.tooltip.gearSet", commandId + 1);
    }

    private static string GetSheetName<T>(uint rowId, Func<T, string> getName)
        where T : struct, Lumina.Excel.IExcelRow<T>
    {
        var row = Svc.Data.GetExcelSheet<T>()?.GetRowOrDefault(rowId);
        return row == null ? string.Empty : getName(row.Value).Trim();
    }

    private static bool IsFallbackLabel(
        string tooltip,
        RaptureHotbarModule.HotbarSlotType type,
        uint commandId) =>
        string.IsNullOrWhiteSpace(tooltip)
        || tooltip == FormatFallback(type, commandId);

    private static string FormatFallback(RaptureHotbarModule.HotbarSlotType type, uint commandId) =>
        $"{type} #{commandId}";

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
}

