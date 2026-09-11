using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace EnhancedQuickPanel.Services;

/// <summary>
/// Single entry point for scratch-slot mutations. Avoid calling ScratchSlot directly elsewhere.
/// HotbarSlot contains Utf8String fields; always mutate the module scratch slot in place.
/// </summary>
internal static unsafe class HotbarScratchSlot
{
    internal delegate void ConfigureScratchSlot(RaptureHotbarModule.HotbarSlot* scratch);

    public static bool TryConfigure(
        RaptureHotbarModule.HotbarSlotType type,
        uint commandId,
        ConfigureScratchSlot configure)
    {
        if (!GameModuleGuard.TryGetHotbar(out var hotbar, out var uiModule))
            return false;

        try
        {
            hotbar->ScratchSlot.Set(uiModule, type, commandId);
            configure(&hotbar->ScratchSlot);
            return true;
        }
        catch (Exception ex)
        {
            PluginServices.Log.Debug($"[EQP] ScratchSlot configure failed ({type} #{commandId}): {ex.Message}");
            return false;
        }
    }

    public static bool TryExecute(RaptureHotbarModule.HotbarSlotType type, uint commandId)
    {
        if (!GameModuleGuard.TryGetHotbar(out var hotbar, out var uiModule))
            return false;

        try
        {
            hotbar->ScratchSlot.Set(uiModule, type, commandId);
            hotbar->ScratchSlot.LoadIconId();
            hotbar->ExecuteSlot(&hotbar->ScratchSlot);
            return true;
        }
        catch (Exception ex)
        {
            PluginServices.Log.Debug($"[EQP] ScratchSlot execute failed ({type} #{commandId}): {ex.Message}");
            return false;
        }
    }
}
