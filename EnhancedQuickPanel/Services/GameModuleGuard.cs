using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace EnhancedQuickPanel.Services;

/// <summary>Guards game access and exposes whether the client is logged in and ready.</summary>
internal static unsafe class GameModuleGuard
{
    public static bool IsClientReady =>
        Svc.ClientState is { IsLoggedIn: true };

    public static bool TryGetHotbar(out RaptureHotbarModule* hotbar, out UIModule* uiModule)
    {
        hotbar = null;
        uiModule = null;
        if (!IsClientReady)
            return false;

        hotbar = RaptureHotbarModule.Instance();
        uiModule = UIModule.Instance();
        return hotbar != null && uiModule != null;
    }

    public static bool TryGetInventory(out InventoryManager* inventory)
    {
        inventory = null;
        if (!IsClientReady)
            return false;

        inventory = InventoryManager.Instance();
        return inventory != null;
    }

    public static bool TryGetMacroModule(out RaptureMacroModule* macroModule)
    {
        macroModule = null;
        if (!IsClientReady)
            return false;

        macroModule = RaptureMacroModule.Instance();
        return macroModule != null;
    }

    public static bool IsQuickPanelModuleAvailable =>
        IsClientReady && QuickPanelModule.Instance() != null;
}

