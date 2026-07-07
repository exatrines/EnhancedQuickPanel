using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace EnhancedQuickPanel.Services;

/// <summary>
/// Single entry point for scratch-slot mutations. Avoid calling ScratchSlot directly elsewhere.
/// </summary>
/// <summary>Provides a temporary hotbar slot used to query game data safely.</summary>
internal static unsafe class ScratchSlotHelper
{
    public static bool TryConfigure(
        RaptureHotbarModule.HotbarSlotType type,
        uint commandId,
        Action<RaptureHotbarModule.HotbarSlot> configure)
    {
        if (!GameModuleGuard.TryGetHotbar(out var hotbar, out var uiModule))
            return false;

        try
        {
            var scratch = hotbar->ScratchSlot;
            scratch.Set(uiModule, type, commandId);
            configure(scratch);
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
            var scratch = hotbar->ScratchSlot;
            scratch.Set(uiModule, type, commandId);
            hotbar->ExecuteSlot(&scratch);
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
            var scratch = hotbar->ScratchSlot;
            scratch.Set(uiModule, type, commandId);

            if (scratch.CommandType == RaptureHotbarModule.HotbarSlotType.Empty || scratch.CommandId == 0)
                return false;

            resolvedType = scratch.CommandType;
            resolvedCommandId = scratch.CommandId;
            return true;
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"[EQP] ScratchSlot resolve failed ({type} #{commandId}): {ex.Message}");
            return false;
        }
    }
}

