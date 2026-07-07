using ECommons.ImGuiMethods;
using EnhancedQuickPanel.Models;

namespace EnhancedQuickPanel.Services;

/// <summary>Handles drag-and-drop reordering of pages in the page list.</summary>
internal static class PageReorderDragHandler
{
    public const string PayloadType = "EQP_PAGE_SWAP";

    /// <summary>Drag payload identifying the page being reordered.</summary>
    private readonly record struct PageDragPayload(int PageIndex);

    private static bool _isDragging;
    private static int _sourceIndex = -1;
    private static int _hoverIndex = -1;

    public static void BeginFrame()
    {
        if (!IsDragging)
            _hoverIndex = -1;
    }

    public static void NotifyGripHeld(int pageIndex, bool gripHeld)
    {
        if (!gripHeld)
            return;

        _isDragging = true;
        _sourceIndex = pageIndex;
    }

    public static void TryBeginDragSource(int pageIndex)
    {
        if (!ImGui.BeginDragDropSource(ImGuiDragDropFlags.SourceNoPreviewTooltip))
            return;

        ImGuiDragDrop.SetDragDropPayload(PayloadType, new PageDragPayload(pageIndex));
        _isDragging = true;
        _sourceIndex = pageIndex;
        ImGui.EndDragDropSource();
    }

    public static void TryAcceptSwapTarget(int targetIndex)
    {
        if (!ImGui.BeginDragDropTarget())
            return;

        if (ImGuiDragDrop.AcceptDragDropPayload<PageDragPayload>(
                PayloadType,
                out var source,
                ImGuiDragDropFlags.AcceptBeforeDelivery | ImGuiDragDropFlags.AcceptNoDrawDefaultRect)
            && source.PageIndex != targetIndex)
            _hoverIndex = targetIndex;

        ImGui.EndDragDropTarget();
    }

    public static bool ShouldHighlightRow(int pageIndex) =>
        pageIndex == _hoverIndex;

    public static bool IsDragging =>
        _isDragging && ImGui.IsMouseDown(ImGuiMouseButton.Left);

    public static bool IsDraggingSourceRow(int pageIndex) =>
        IsDragging && pageIndex == _sourceIndex;

    public static void ProcessEndOfFrame(IList<PanelPage> pages, ref int selectedPage)
    {
        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left)
            && _isDragging
            && _hoverIndex >= 0
            && _sourceIndex >= 0
            && _sourceIndex != _hoverIndex)
        {
            SwapPages(pages, _sourceIndex, _hoverIndex);
            UpdateSelectedPage(ref selectedPage, _sourceIndex, _hoverIndex);
            EzConfig.Save();
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            _isDragging = false;
            _sourceIndex = -1;
            _hoverIndex = -1;
        }
    }

    private static void SwapPages(IList<PanelPage> pages, int sourceIndex, int targetIndex)
    {
        var source = pages[sourceIndex];
        pages[sourceIndex] = pages[targetIndex];
        pages[targetIndex] = source;
    }

    private static void UpdateSelectedPage(ref int selectedPage, int sourceIndex, int targetIndex)
    {
        if (selectedPage == sourceIndex)
            selectedPage = targetIndex;
        else if (selectedPage == targetIndex)
            selectedPage = sourceIndex;
    }
}

