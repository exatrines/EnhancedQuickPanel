using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace EnhancedQuickPanel.Services;

/// <summary>
/// Single entry point for scratch-slot mutations. Avoid calling ScratchSlot directly elsewhere.
/// HotbarSlot contains Utf8String fields; always mutate the module scratch slot in place.
/// </summary>
internal static unsafe class ScratchSlotHelper
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
            PluginLog.Debug($"[EQP] ScratchSlot configure failed ({type} #{commandId}): {ex.Message}");
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
            PluginLog.Debug($"[EQP] ScratchSlot execute failed ({type} #{commandId}): {ex.Message}");
            return false;
        }
    }

    public static bool TryResolveCommand(
        RaptureHotbarModule.HotbarSlotType type,
        uint commandId,
        out RaptureHotbarModule.HotbarSlotType resolvedType,
        out uint resolvedCommandId)
    {
        resolvedType = RaptureHotbarModule.HotbarSlotType.Empty;
        resolvedCommandId = 0;

        if (!GameModuleGuard.TryGetHotbar(out var hotbar, out var uiModule))
            return false;

        try
        {
            hotbar->ScratchSlot.Set(uiModule, type, commandId);

            if (hotbar->ScratchSlot.CommandType == RaptureHotbarModule.HotbarSlotType.Empty
                || hotbar->ScratchSlot.CommandId == 0)
                return false;

            resolvedType = hotbar->ScratchSlot.CommandType;
            resolvedCommandId = hotbar->ScratchSlot.CommandId;
            return true;
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"[EQP] ScratchSlot resolve failed ({type} #{commandId}): {ex.Message}");
            return false;
        }
    }
}
