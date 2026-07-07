using FFXIVClientStructs.FFXIV.Client.UI;

namespace EnhancedQuickPanel.Services;

/// <summary>
/// Per-frame cache for native QuickPanel addon traversal.
/// </summary>
/// <summary>Caches native quick panel UI node data read from the game.</summary>
internal static unsafe class QuickPanelUiCache
{
    private static int _cachedFrame = -1;
    private static nint[] _dragDropPointers = [];

    public static void Invalidate()
    {
        _cachedFrame = -1;
        _dragDropPointers = [];
    }

    public static bool TryGetDragDropAt(int slotIndex, out nint dragDrop)
    {
        dragDrop = 0;
        if (!GameModuleGuard.IsClientReady)
            return false;

        EnsureCurrentFrameCache();
        if (slotIndex < 0 || slotIndex >= _dragDropPointers.Length)
            return false;

        dragDrop = _dragDropPointers[slotIndex];
        return dragDrop != 0;
    }

    private static void EnsureCurrentFrameCache()
    {
        var frame = ImGui.GetFrameCount();
        if (_cachedFrame == frame)
            return;

        _cachedFrame = frame;
        _dragDropPointers = [];

        if (!QuickPanelAddon.TryGetAddon(out var addon) || !GenericHelpers.IsAddonReady(addon))
            return;

        try
        {
            _dragDropPointers = NativeQuickPanelUiReader.CollectDragDropPointers(addon);
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"[EQP] QuickPanel UI cache rebuild failed: {ex.Message}");
            _dragDropPointers = [];
        }
    }
}

