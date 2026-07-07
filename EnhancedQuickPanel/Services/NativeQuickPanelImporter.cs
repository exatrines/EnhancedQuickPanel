using EnhancedQuickPanel.Models;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace EnhancedQuickPanel.Services;

/// <summary>Copies slots from the game's native quick panel into plugin pages.</summary>
internal static class NativeQuickPanelImporter
{
    public static bool TryImportAllPages(IList<PanelPage> pages)
    {
        if (!NativeQuickPanelReader.IsAvailable)
            return false;

        for (var pageIndex = 0; pageIndex < Configuration.NativePageCount; pageIndex++)
        {
            while (pages.Count <= pageIndex)
                pages.Add(Configuration.CreateEmptyPage(T("page.defaultName", pages.Count + 1)));

            ImportNativePage(pages[pageIndex], pageIndex);
        }

        return true;
    }

    public static bool TryImportNativePageAsNewPage(int nativePageIndex, out int newPageIndex)
    {
        newPageIndex = -1;
        if (!NativeQuickPanelReader.IsAvailable)
            return false;

        if (nativePageIndex is < 0 or >= Configuration.NativePageCount)
            return false;

        C.EnsureDefaults();
        var page = Configuration.CreateEmptyPage(GetNativeImportPageName(nativePageIndex));
        ImportNativeSlots(page, nativePageIndex);
        C.Pages.Add(page);
        newPageIndex = C.Pages.Count - 1;
        return true;
    }

    public static bool TryBuildNativePagePreview(int nativePageIndex, out PanelPage page)
    {
        page = Configuration.CreateEmptyPage();
        if (!NativeQuickPanelReader.IsAvailable)
            return false;

        if (nativePageIndex is < 0 or >= Configuration.NativePageCount)
            return false;

        ImportNativeSlots(page, nativePageIndex);
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

    private static void ImportNativePage(PanelPage panelPage, int nativePageIndex)
    {
        panelPage.Name = GetNativeImportPageName(nativePageIndex);
        ImportNativeSlots(panelPage, nativePageIndex);
    }

    private static void ImportNativeSlots(PanelPage panelPage, int nativePageIndex)
    {
        panelPage.Slots.Clear();
        for (var index = 0; index < Configuration.SlotsPerPage; index++)
        {
            panelPage.Slots.Add(
                NativeQuickPanelReader.TryGetSlot(nativePageIndex, index, out var type, out var commandId)
                    ? FromNativeSlot(type, commandId)
                    : new PanelSlot());
        }
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
