using EnhancedQuickPanel.Models;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace EnhancedQuickPanel.Services;

/// <summary>Copies slots from the game's native quick panel into plugin pages.</summary>
internal static class NativeQuickPanelImporter
{
    public static bool TryImportNativePageAsNewPage(int nativePageIndex, out int newPageIndex)
    {
        newPageIndex = -1;
        if (!NativeQuickPanelReader.IsAvailable)
            return false;

        if (nativePageIndex is < 0 or >= Configuration.NativePageCount)
            return false;

        Config.EnsureDefaults();
        var page = Configuration.CreateEmptyPage(Config.SlotsPerPage, GetNativeImportPageName(nativePageIndex));
        ImportNativeSlots(page, nativePageIndex);
        Config.Pages.Add(page);
        newPageIndex = Config.Pages.Count - 1;
        return true;
    }

    public static bool TryBuildNativePagePreview(int nativePageIndex, out PanelPage page)
    {
        page = Configuration.CreateEmptyPage(Configuration.NativeSlotsPerPage);
        if (!NativeQuickPanelReader.IsAvailable)
            return false;

        if (nativePageIndex is < 0 or >= Configuration.NativePageCount)
            return false;

        FillNativePreviewPage(page, nativePageIndex);
        return true;
    }

    private static string GetNativeImportPageName(int pageIndex) =>
        pageIndex switch
        {
            0 => T("page.nativeImportName.1"),
            1 => T("page.nativeImportName.2"),
            2 => T("page.nativeImportName.3"),
            3 => T("page.nativeImportName.4"),
            _ => T("page.nativeImportName", pageIndex + 1),
        };

    private static void ImportNativeSlots(PanelPage panelPage, int nativePageIndex)
    {
        Config.EnsureDefaults();
        panelPage.Slots ??= [];
        while (panelPage.Slots.Count < Config.SlotsPerPage)
            panelPage.Slots.Add(new PanelSlot());

        PanelLayout.PasteTopLeft(
            panelPage.Slots,
            Config.GridColumns,
            Config.GridRows,
            ReadNativeSlots(nativePageIndex),
            Configuration.GridSize,
            Configuration.GridSize);
    }

    private static void FillNativePreviewPage(PanelPage panelPage, int nativePageIndex)
    {
        panelPage.Slots.Clear();
        panelPage.Slots.AddRange(ReadNativeSlots(nativePageIndex));
    }

    private static List<PanelSlot> ReadNativeSlots(int nativePageIndex)
    {
        var slots = new List<PanelSlot>(Configuration.NativeSlotsPerPage);
        for (var index = 0; index < Configuration.NativeSlotsPerPage; index++)
        {
            slots.Add(
                NativeQuickPanelReader.TryGetSlot(nativePageIndex, index, out var type, out var commandId)
                    ? FromNativeSlot(type, commandId)
                    : new PanelSlot());
        }

        return slots;
    }

    internal static PanelSlot FromHotbarSlot(RaptureHotbarModule.HotbarSlotType type, uint commandId) =>
        FromNativeSlot(type, commandId);

    private static PanelSlot FromNativeSlot(RaptureHotbarModule.HotbarSlotType type, uint commandId)
    {
        if (type == RaptureHotbarModule.HotbarSlotType.Macro)
        {
            if (!MacroSlotResolver.TryResolveImportMacro(type, commandId, out var macroSet, out var macroIndex))
                return new PanelSlot();

            return new PanelSlot
            {
                Kind = PanelSlotKind.Macro,
                MacroSet = macroSet,
                MacroIndex = macroIndex,
                CommandId = commandId,
            };
        }

        return new PanelSlot
        {
            Kind = PanelSlotKind.Action,
            CommandType = (byte)type,
            CommandId = commandId,
        };
    }
}
