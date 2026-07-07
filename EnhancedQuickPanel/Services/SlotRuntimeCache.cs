using EnhancedQuickPanel.Models;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace EnhancedQuickPanel.Services;

/// <summary>Cache key identifying a slot by command type and command ID.</summary>
internal readonly record struct SlotRuntimeKey(byte CommandType, uint CommandId);

/// <summary>Cached per-frame runtime state for a slot.</summary>
internal readonly record struct SlotRuntimeState(
    bool IsUsable,
    SlotCooldownInfo Cooldown,
    int ItemQuantity,
    bool ShowActionCharges,
    int ActionCharges)
{
    public static SlotRuntimeState Default => new(true, SlotCooldownInfo.None, 0, false, 0);
}

/// <summary>
/// Per-frame cache that reads scratch-slot runtime state once per action key.
/// </summary>
/// <summary>Caches per-frame runtime state (availability, cooldown, charges) for slots.</summary>
internal static unsafe class SlotRuntimeCache
{
    private static int _cachedFrame = -1;
    private static readonly Dictionary<SlotRuntimeKey, SlotRuntimeState> Cache = new();

    public static void Invalidate()
    {
        _cachedFrame = -1;
        Cache.Clear();
    }

    public static SlotRuntimeState Get(PanelSlot slot, ResolvedSlotIcon icon)
    {
        if (slot.Kind != PanelSlotKind.Action || slot.CommandId == 0)
            return SlotRuntimeState.Default;

        var type = (RaptureHotbarModule.HotbarSlotType)slot.CommandType;
        if (type is RaptureHotbarModule.HotbarSlotType.Empty
            or RaptureHotbarModule.HotbarSlotType.Macro)
        {
            return SlotRuntimeState.Default;
        }

        return Get(type, slot.CommandId, icon);
    }

    public static SlotRuntimeState Get(
        RaptureHotbarModule.HotbarSlotType type,
        uint commandId,
        ResolvedSlotIcon icon)
    {
        if (type is RaptureHotbarModule.HotbarSlotType.Empty
            or RaptureHotbarModule.HotbarSlotType.Macro
            || commandId == 0)
        {
            return SlotRuntimeState.Default;
        }

        EnsureCurrentFrame();
        var key = new SlotRuntimeKey((byte)type, commandId);
        if (Cache.TryGetValue(key, out var state))
            return state;

        state = Build(type, commandId, icon);
        Cache[key] = state;
        return state;
    }

    private static void EnsureCurrentFrame()
    {
        var frame = ImGui.GetFrameCount();
        if (_cachedFrame == frame)
            return;

        _cachedFrame = frame;
        Cache.Clear();
    }

    private static SlotRuntimeState Build(
        RaptureHotbarModule.HotbarSlotType type,
        uint commandId,
        ResolvedSlotIcon icon)
    {
        if (!GameModuleGuard.TryGetHotbar(out var hotbar, out var uiModule))
            return SlotRuntimeState.Default;

        try
        {
            var scratch = hotbar->ScratchSlot;
            scratch.Set(uiModule, type, commandId);

            if (scratch.CommandType == RaptureHotbarModule.HotbarSlotType.Empty || scratch.CommandId == 0)
                return new SlotRuntimeState(false, SlotCooldownInfo.None, 0, false, 0);

            var appearance = SlotAvailabilityResolver.ResolveAppearance(scratch, hotbar);
            var isUsable = appearance.SlotType != RaptureHotbarModule.HotbarSlotType.Empty
                && appearance.ActionId != 0
                && scratch.IsSlotUsable(appearance.SlotType, appearance.ActionId);
            var cooldown = SlotCooldownResolver.ResolveScratch(scratch);
            var quantity = InventorySlotHelper.IsItemSlotType(type)
                ? SlotOverlayResolver.ResolveItemQuantityScratch(
                    scratch,
                    hotbar,
                    type,
                    commandId,
                    icon,
                    appearance)
                : 0;
            var (showCharges, charges) = SlotChargeResolver.Resolve(scratch, appearance, type);

            return new SlotRuntimeState(isUsable, cooldown, quantity, showCharges, charges);
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"[EQP] Slot runtime read failed ({type} #{commandId}): {ex.Message}");
            return SlotRuntimeState.Default;
        }
    }
}

