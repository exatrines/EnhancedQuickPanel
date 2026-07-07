using ECommons.ImGuiMethods;
using EnhancedQuickPanel.Models;

namespace EnhancedQuickPanel.Services;

/// <summary>Handles drag-and-drop swapping of slots within the overlay while in edit mode.</summary>
internal static class SlotSwapDragHandler
{
    public const string PayloadType = "EQP_SLOT_SWAP";

    /// <summary>Drag payload identifying the source page and slot index.</summary>
    private readonly record struct SlotDragPayload(int Page, int Index);

    private static bool _isDragging;
    private static int _sourcePage = -1;
    private static int _sourceIndex = -1;
    private static int _hoverPage = -1;
    private static int _hoverSlotIndex = -1;
    private static bool _wasDragging;
    private static bool _completedSwap;
    private static int _completedSwapPage = -1;
    private static int _completedSwapIndex = -1;

    public static bool TryGetDragSource(out int page, out int index)
    {
        if (_isDragging && _sourcePage >= 0 && _sourceIndex >= 0)
        {
            page = _sourcePage;
            index = _sourceIndex;
            return true;
        }

        page = -1;
        index = -1;
        return false;
    }

    public static bool TryConsumeCompletedSwapTarget(out int page, out int index)
    {
        if (!_completedSwap)
        {
            page = -1;
            index = -1;
            return false;
        }

        _completedSwap = false;
        page = _completedSwapPage;
        index = _completedSwapIndex;
        _completedSwapPage = -1;
        _completedSwapIndex = -1;
        return true;
    }

    public static bool IsSourceSlot(int page, int slotIndex) =>
        _isDragging && _sourcePage == page && _sourceIndex == slotIndex;

    public static void BeginFrame()
    {
        _hoverPage = -1;
        _hoverSlotIndex = -1;
    }

    public static void TryBeginDragSource(int page, int slotIndex, bool enabled)
    {
        if (!enabled || SlotDragDropHandler.IsGameDragActive)
            return;

        if (!ImGui.BeginDragDropSource(ImGuiDragDropFlags.SourceNoPreviewTooltip))
            return;

        ImGuiDragDrop.SetDragDropPayload(PayloadType, new SlotDragPayload(page, slotIndex));
        _isDragging = true;
        _sourcePage = page;
        _sourceIndex = slotIndex;
        ImGui.EndDragDropSource();
    }

    public static void TryAcceptSwapTarget(int targetPage, int targetSlotIndex)
    {
        if (!ImGui.BeginDragDropTarget())
            return;

        if (ImGuiDragDrop.AcceptDragDropPayload<SlotDragPayload>(
                PayloadType,
                out var source,
                ImGuiDragDropFlags.AcceptBeforeDelivery | ImGuiDragDropFlags.AcceptNoDrawDefaultRect)
            && source.Page == targetPage
            && source.Index != targetSlotIndex)
        {
            _hoverPage = targetPage;
            _hoverSlotIndex = targetSlotIndex;
        }

        ImGui.EndDragDropTarget();
    }

    public static bool ShouldHighlightSwapTarget(int page, int slotIndex) =>
        page == _hoverPage && slotIndex == _hoverSlotIndex;

    public static bool ShouldSuppressSlotActivation() =>
        IsInternalDragActive || _wasDragging;

    public static bool IsInternalDragActive =>
        _isDragging && ImGui.IsMouseDown(ImGuiMouseButton.Left);

    public static void ProcessEndOfFrame()
    {
        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left)
            && _isDragging
            && _hoverPage >= 0
            && _hoverSlotIndex >= 0
            && _sourcePage == _hoverPage
            && _sourceIndex >= 0
            && _sourceIndex != _hoverSlotIndex)
        {
            var page = C.Pages[_hoverPage];
            SwapSlotContents(page.Slots[_sourceIndex], page.Slots[_hoverSlotIndex]);
            EzConfig.Save();
            _completedSwap = true;
            _completedSwapPage = _hoverPage;
            _completedSwapIndex = _hoverSlotIndex;
        }

        _wasDragging = _isDragging;

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            _isDragging = false;
            _sourcePage = -1;
            _sourceIndex = -1;
            _hoverPage = -1;
            _hoverSlotIndex = -1;
        }
    }

    private static void SwapSlotContents(PanelSlot left, PanelSlot right)
    {
        (left.Kind, right.Kind) = (right.Kind, left.Kind);
        (left.CommandType, right.CommandType) = (right.CommandType, left.CommandType);
        (left.CommandId, right.CommandId) = (right.CommandId, left.CommandId);
        (left.MacroSet, right.MacroSet) = (right.MacroSet, left.MacroSet);
        (left.MacroIndex, right.MacroIndex) = (right.MacroIndex, left.MacroIndex);
        (left.IconId, right.IconId) = (right.IconId, left.IconId);
        (left.Label, right.Label) = (right.Label, left.Label);
        (left.TextBody, right.TextBody) = (right.TextBody, left.TextBody);
    }
}

