using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using EnhancedQuickPanel.Models;
using EnhancedQuickPanel.Services;

namespace EnhancedQuickPanel.UI;

/// <summary>Sidebar panel that lists pages in edit mode with add, remove, rename, and reorder controls.</summary>
internal static class PageListPanel
{
    private const int MaxVisibleRows = 8;

    private static string _newPageDraft = string.Empty;

    public static bool Draw(
        ref int selectedPage,
        IList<PanelPage> pages,
        PanelUiStyleConfig style,
        float width,
        float? totalHeight = null)
    {
        C.EnsureDefaults();
        if (pages.Count == 0)
            return false;

        selectedPage = Math.Clamp(selectedPage, 0, pages.Count - 1);

        var actionButtonWidth = ImGui.GetFrameHeight();
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var rowStep = ImGui.GetFrameHeightWithSpacing();
        var addRowHeight = rowStep + ImGui.GetStyle().ItemSpacing.Y + 1f;
        var scrollHeight = totalHeight.HasValue
            ? Math.Max(rowStep, totalHeight.Value - addRowHeight)
            : rowStep * Math.Min(pages.Count, MaxVisibleRows) - ImGui.GetStyle().ItemSpacing.Y;

        DrawAddPageRow(ref selectedPage, pages, actionButtonWidth, spacing, style, width);
        ImGui.Separator();

        var pageRemoved = false;
        using (var scroll = ImRaii.Child("##eqpPageListScroll", new Vector2(width, scrollHeight), false))
        {
            if (scroll)
            {
                PageReorderDragHandler.BeginFrame();

                for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
                {
                    if (DrawRow(ref selectedPage, pages, pageIndex, actionButtonWidth, spacing, style))
                    {
                        pageRemoved = true;
                        break;
                    }
                }
            }
        }

        return pageRemoved;
    }

    private static bool DrawRow(
        ref int selectedPage,
        IList<PanelPage> pages,
        int pageIndex,
        float actionButtonWidth,
        float spacing,
        PanelUiStyleConfig style)
    {
        var actionSize = new Vector2(actionButtonWidth, actionButtonWidth);
        var rowHeight = ImGui.GetFrameHeight();
        var rowWidth = ImGui.GetContentRegionAvail().X;
        var rowStart = ImGui.GetCursorScreenPos();

        CenteredIconButton.Draw(
            FontAwesomeIcon.Bars,
            $"##eqpPageListGrip{pageIndex}",
            actionSize,
            style.TextColor,
            style.TextHoverColor);

        PageReorderDragHandler.NotifyGripHeld(
            pageIndex,
            ImGui.IsItemActive() && ImGui.IsMouseDown(ImGuiMouseButton.Left));
        PageReorderDragHandler.TryBeginDragSource(pageIndex);
        PageReorderDragHandler.TryAcceptSwapTarget(pageIndex);

        ImGui.SameLine(0, spacing);

        var nameInputWidth = Math.Max(
            64f,
            rowWidth - actionButtonWidth * 2f - spacing * 2f);
        ImGui.SetNextItemWidth(nameInputWidth);

        var pageName = pages[pageIndex].Name ?? string.Empty;
        using (new PanelUiEditFieldStyleScope(style))
        using (PanelUiTextStyle.PushInputText(style, $"##eqpPageListName{pageIndex}"))
        {
            if (ImGui.InputText($"##eqpPageListName{pageIndex}", ref pageName, 64))
            {
                pages[pageIndex].Name = pageName;
                EzConfig.Save();
            }

            PanelUiTextStyle.NotifyInputHover($"##eqpPageListName{pageIndex}");
        }

        PageReorderDragHandler.TryAcceptSwapTarget(pageIndex);

        ImGui.SameLine(0, spacing);

        var ctrlHeld = ImGui.GetIO().KeyCtrl;
        var removable = C.CanRemovePage(pageIndex) && !PageReorderDragHandler.IsDragging;
        var canDelete = removable && ctrlHeld;
        var deleteHint = removable && !ctrlHeld ? T("common.deleteHint") : null;
        if (CenteredIconButton.Draw(
                FontAwesomeIcon.Trash,
                $"##eqpPageListDelete{pageIndex}",
                actionSize,
                style.TextColor,
                style.TextHoverColor,
                enabled: canDelete,
                disabledTooltip: deleteHint)
            && canDelete
            && C.TryRemovePage(pageIndex))
        {
            selectedPage = Math.Clamp(selectedPage, 0, pages.Count - 1);
            EzConfig.Save();
            return true;
        }

        PageReorderDragHandler.TryAcceptSwapTarget(pageIndex);

        if (PageReorderDragHandler.ShouldHighlightRow(pageIndex))
            DrawRowSwapHighlight(rowStart, new Vector2(rowWidth, rowHeight));

        if (PageReorderDragHandler.IsDraggingSourceRow(pageIndex))
            DrawRowDragSelectionBorder(rowStart, new Vector2(rowWidth, rowHeight));

        return false;
    }

    private static void DrawAddPageRow(
        ref int selectedPage,
        IList<PanelPage> pages,
        float actionButtonWidth,
        float spacing,
        PanelUiStyleConfig style,
        float width)
    {
        var actionSize = new Vector2(actionButtonWidth, actionButtonWidth);
        var inputWidth = Math.Max(
            64f,
            width - actionButtonWidth - spacing);

        ImGui.SetNextItemWidth(inputWidth);
        using (new PanelUiEditFieldStyleScope(style))
        using (PanelUiTextStyle.PushInputText(style, "##eqpPageListNewName"))
        {
            if (ImGui.InputTextWithHint("##eqpPageListNewName", T("page.nameHint"), ref _newPageDraft, 64))
            { }

            PanelUiTextStyle.NotifyInputHover("##eqpPageListNewName");
        }

        ImGui.SameLine(0, spacing);
        if (!CenteredIconButton.Draw(
                FontAwesomeIcon.Plus,
                "##eqpPageListAdd",
                actionSize,
                style.TextColor,
                style.TextHoverColor))
            return;

        var pageName = string.IsNullOrWhiteSpace(_newPageDraft)
            ? C.GetNextPageName()
            : _newPageDraft.Trim();
        C.AddPage(pageName);
        selectedPage = pages.Count - 1;
        _newPageDraft = string.Empty;
        EzConfig.Save();
    }

    private static void DrawRowSwapHighlight(Vector2 min, Vector2 size)
    {
        var drawList = ImGui.GetWindowDrawList();
        var max = min + size;
        var color = ImGui.ColorConvertFloat4ToU32(C.SlotDropTargetColor);
        drawList.AddRectFilled(min, max, color, ImGui.GetStyle().FrameRounding);
    }

    private static void DrawRowDragSelectionBorder(Vector2 min, Vector2 size)
    {
        var drawList = ImGui.GetWindowDrawList();
        var max = min + size;
        var color = ImGui.ColorConvertFloat4ToU32(C.SelectedSlotBorderColor);
        drawList.AddRect(min, max, color, ImGui.GetStyle().FrameRounding, ImDrawFlags.None, 2f);
    }
}

