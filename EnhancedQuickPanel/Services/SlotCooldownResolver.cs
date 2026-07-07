using EnhancedQuickPanel.Models;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace EnhancedQuickPanel.Services;

/// <summary>Computes the remaining cooldown for a slot's action.</summary>
internal static unsafe class SlotCooldownResolver
{
    private const byte InvalidActionTypeByte = unchecked((byte)0xFFFFFFFF);

    public static SlotCooldownInfo Resolve(PanelSlot slot)
    {
        if (slot.Kind != PanelSlotKind.Action || slot.CommandId == 0)
            return SlotCooldownInfo.None;

        var type = (RaptureHotbarModule.HotbarSlotType)slot.CommandType;
        if (type is RaptureHotbarModule.HotbarSlotType.Empty
            or RaptureHotbarModule.HotbarSlotType.Macro)
        {
            return SlotCooldownInfo.None;
        }

        return Resolve(type, slot.CommandId);
    }

    public static SlotCooldownInfo ResolveScratch(RaptureHotbarModule.HotbarSlot scratch)
    {
        try
        {
            var secondsLeft = 0;
            var percent = scratch.GetSlotActionCooldownPercentage(&secondsLeft);
            if (percent <= 0 && secondsLeft <= 0)
                return SlotCooldownInfo.None;

            var remainingSeconds = ResolveRemainingSeconds(scratch, secondsLeft, percent);
            if (remainingSeconds <= 0.05f)
                return SlotCooldownInfo.None;

            var remainingFraction = ResolveRemainingFraction(scratch, percent, remainingSeconds);
            return new SlotCooldownInfo(remainingFraction, remainingSeconds);
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"[EQP] Cooldown read failed (scratch): {ex.Message}");
            return SlotCooldownInfo.None;
        }
    }

    public static SlotCooldownInfo Resolve(RaptureHotbarModule.HotbarSlotType type, uint commandId)
    {
        if (!GameModuleGuard.TryGetHotbar(out var hotbar, out var uiModule))
            return SlotCooldownInfo.None;

        try
        {
            var scratch = hotbar->ScratchSlot;
            scratch.Set(uiModule, type, commandId);

            if (scratch.CommandType == RaptureHotbarModule.HotbarSlotType.Empty || scratch.CommandId == 0)
                return SlotCooldownInfo.None;

            return ResolveScratch(scratch);
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"[EQP] Cooldown read failed ({type} #{commandId}): {ex.Message}");
            return SlotCooldownInfo.None;
        }
    }

    private static float ResolveRemainingSeconds(
        RaptureHotbarModule.HotbarSlot scratch,
        int secondsLeft,
        int percent)
    {
        if (secondsLeft > 0)
            return secondsLeft;

        if (!TryGetRecastState(scratch, out var actionType, out var actionId, out var total, out var elapsed))
        {
            return percent > 0
                ? EstimateSecondsFromPercent(percent, 2.5f)
                : 0f;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager != null && actionManager->IsRecastTimerActive(actionType, actionId))
        {
            var remaining = Math.Max(0f, total - elapsed);
            if (remaining > 0f)
                return remaining;
        }

        if (total > 0f && elapsed > 0f)
            return Math.Max(0f, total - elapsed);

        return percent > 0
            ? EstimateSecondsFromPercent(percent, total > 0f ? total : 2.5f)
            : 0f;
    }

    private static float ResolveRemainingFraction(
        RaptureHotbarModule.HotbarSlot scratch,
        int percent,
        float remainingSeconds)
    {
        if (TryGetRecastState(scratch, out _, out _, out var total, out var elapsed) && total > 0f)
            return Math.Clamp((total - elapsed) / total, 0f, 1f);

        if (percent is > 0 and < 100)
            return Math.Clamp((100 - percent) / 100f, 0f, 1f);

        if (remainingSeconds > 0f)
            return 1f;

        return 0f;
    }

    private static float EstimateSecondsFromPercent(int percent, float totalSeconds) =>
        Math.Max(0.1f, totalSeconds * Math.Clamp((100 - percent) / 100f, 0f, 1f));

    private static bool TryGetRecastState(
        RaptureHotbarModule.HotbarSlot scratch,
        out ActionType actionType,
        out uint actionId,
        out float total,
        out float elapsed)
    {
        actionType = default;
        actionId = 0;
        total = 0f;
        elapsed = 0f;

        var slotType = scratch.ApparentSlotType != RaptureHotbarModule.HotbarSlotType.Empty
            ? scratch.ApparentSlotType
            : scratch.CommandType;
        if (!TryResolveActionType(scratch, slotType, out actionType)
            && !TryResolveActionType(scratch, scratch.CommandType, out actionType))
        {
            return false;
        }

        actionId = scratch.ApparentActionId != 0 ? scratch.ApparentActionId : scratch.CommandId;
        if (actionId == 0)
            return false;

        var actionManager = ActionManager.Instance();
        if (actionManager == null)
            return false;

        total = actionManager->GetRecastTime(actionType, actionId);
        elapsed = actionManager->GetRecastTimeElapsed(actionType, actionId);
        return total > 0f || elapsed > 0f;
    }

    private static bool TryResolveActionType(
        RaptureHotbarModule.HotbarSlot scratch,
        RaptureHotbarModule.HotbarSlotType slotType,
        out ActionType actionType)
    {
        actionType = scratch.GetActionTypeForSlotType(slotType);
        return (byte)actionType != InvalidActionTypeByte;
    }
}

