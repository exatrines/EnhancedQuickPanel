using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace EnhancedQuickPanel.Services;

/// <summary>Reads raw slot type and command IDs from the native quick panel module.</summary>
internal static unsafe class NativeQuickPanelReader
{
    public static bool IsAvailable => GameModuleGuard.IsQuickPanelModuleAvailable;

    public static bool TryGetSlot(int page, int index, out RaptureHotbarModule.HotbarSlotType type, out uint commandId)
    {
        if (!TryGetSlotRaw(page, index, out type, out commandId))
            return false;

        return type != RaptureHotbarModule.HotbarSlotType.Empty && commandId != 0;
    }

    public static bool TryGetSlotRaw(int page, int index, out RaptureHotbarModule.HotbarSlotType type, out uint commandId)
    {
        type = RaptureHotbarModule.HotbarSlotType.Empty;
        commandId = 0;

        var module = QuickPanelModule.Instance();
        if (module == null || page is < 0 or >= Configuration.NativePageCount || index is < 0 or >= Configuration.SlotsPerPage)
            return false;

        GetPageData(module, page, out var types, out var ids);
        type = types[index];
        commandId = ids[index];
        return true;
    }

    public static bool IsSlotEmpty(int page, int index)
    {
        if (!TryGetSlotRaw(page, index, out var type, out var commandId))
            return true;

        return type == RaptureHotbarModule.HotbarSlotType.Empty || commandId == 0;
    }

    private static void GetPageData(
        QuickPanelModule* module,
        int page,
        out Span<RaptureHotbarModule.HotbarSlotType> types,
        out Span<uint> ids)
    {
        switch (page)
        {
            case 0:
                types = module->Panel0CommandTypes;
                ids = module->Panel0CommandIds;
                return;
            case 1:
                types = module->Panel1CommandTypes;
                ids = module->Panel1CommandIds;
                return;
            case 2:
                types = module->Panel2CommandTypes;
                ids = module->Panel2CommandIds;
                return;
            case 3:
                types = module->Panel3CommandTypes;
                ids = module->Panel3CommandIds;
                return;
            default:
                types = [];
                ids = [];
                return;
        }
    }
}

