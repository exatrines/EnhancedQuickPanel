using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using EnhancedQuickPanel.Models;
using EnhancedQuickPanel.Services;

namespace EnhancedQuickPanel.UI;

/// <summary>Draws the overlay's page-name bar and handles page switching via the click popup and mouse wheel.</summary>
internal static class PageSelectorBar
{
    private const string PagePopupId = "##eqpPageSelectorPopup";

    private const int PagePopupStyleVarCount = 3;
    private const int PagePopupScrollAfterRows = 5;

    /// <summary>Precomputed sizes used to lay out the page selector bar.</summary>
    private readonly record struct PageBarMetrics(
        float? TotalWidth,
        float? SelectorWidth,
        float ActionButtonWidth,
        float ItemSpacing);

    /// <summary>Precomputed geometry for the page selector popup.</summary>
    private readonly record struct PopupLayout(
        Vector2 Position,
        float TotalWidth,
        float ActionButtonWidth,
        float ItemSpacing,
        float ContentInsetX);

    public static void Draw(
        ref int selectedPage,
        IList<PanelPage> pages,
        ref bool isEditingPageName,
        PanelUiStyleConfig style,
        float? barWidth = null,
        bool showSeparatorBefore = false,
        bool showSeparatorAfter = false,
        bool showAddPageButton = false,
        bool showPenButton = true,
        float pagePopupXOffset = 0f)
    {
        C.EnsureDefaults();
        if (pages.Count == 0)
            return;

        selectedPage = Math.Clamp(selectedPage, 0, pages.Count - 1);

        if (showSeparatorBefore)
            ImGui.Separator();

        var metrics = CreateMetrics(barWidth, showAddPageButton, showPenButton);

        using (new PanelUiButtonStyleScope(style))
            DrawSelectorRow(
                ref selectedPage,
                pages,
                ref isEditingPageName,
                style,
                metrics,
                showPenButton);

        DrawPagePopup(
            ref selectedPage,
            pages,
            CreatePopupLayout(metrics, isEditingPageName ? pagePopupXOffset : 0f));

        if (showSeparatorAfter)
            ImGui.Separator();
    }

    private static PageBarMetrics CreateMetrics(float? barWidth, bool showAddPageButton = false, bool showPenButton = true)
    {
        var actionButtonWidth = ImGui.GetFrameHeight();
        var rowGap = ImGui.GetStyle().ItemSpacing.X;

        if (!barWidth.HasValue)
            return new PageBarMetrics(null, null, actionButtonWidth, rowGap);

        var iconButtonCount = showPenButton ? 1 : 0;
        var gapCount = iconButtonCount;
        var selectorWidth = Math.Max(
            64f,
            barWidth.Value - actionButtonWidth * iconButtonCount - rowGap * gapCount);
        return new PageBarMetrics(barWidth.Value, selectorWidth, actionButtonWidth, rowGap);
    }

    private static void DrawSelectorRow(
        ref int selectedPage,
        IList<PanelPage> pages,
        ref bool isEditingPageName,
        PanelUiStyleConfig style,
        PageBarMetrics metrics,
        bool showPenButton)
    {
        var rowHeight = ImGui.GetFrameHeight();

        var selectorSize = metrics.SelectorWidth.HasValue
            ? new Vector2(metrics.SelectorWidth.Value, rowHeight)
            : new Vector2(0f, rowHeight);

        DrawPageLabel(ref selectedPage, pages, selectorSize);

        if (!showPenButton)
            return;

        ImGui.SameLine(0, metrics.ItemSpacing);
        DrawPenButton(ref isEditingPageName, style, metrics.ActionButtonWidth, rowHeight);
    }

    private static void DrawPageLabel(
        ref int selectedPage,
        IList<PanelPage> pages,
        Vector2 size)
    {
        var displayName = GetDisplayName(pages[selectedPage], selectedPage);

        if (ImGui.Button($"{displayName}##eqpPageSelector", size)
            && (C.ShowPageSelectorPopup ?? true))
            ImGui.OpenPopup(PagePopupId);

        if (ImGui.IsItemHovered())
            HandleMouseWheel(ref selectedPage, pages.Count);
    }

    private static void DrawPenButton(
        ref bool isEditingPageName,
        PanelUiStyleConfig style,
        float actionButtonWidth,
        float rowHeight)
    {
        var size = new Vector2(actionButtonWidth, actionButtonWidth);

        using (isEditingPageName
            ? ImRaii.PushColor(ImGuiCol.Button, style.ButtonBgHoverColor)
            : null)
        {
            if (CenteredIconButton.Draw(
                    FontAwesomeIcon.Pen,
                    "##eqpEditPage",
                    size,
                    style.TextColor,
                    style.TextHoverColor))
            {
                isEditingPageName = !isEditingPageName;
                if (!isEditingPageName)
                    ImGui.CloseCurrentPopup();
            }
        }
    }

    private static PopupLayout CreatePopupLayout(PageBarMetrics metrics, float popupXOffset = 0f)
    {
        var rowMax = ImGui.GetItemRectMax();
        var padding = C.WindowPadding;
        var windowPos = ImGui.GetWindowPos();
        var popupWidth = metrics.TotalWidth
            ?? Math.Max(64f, ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X);

        return new PopupLayout(
            new Vector2(windowPos.X + padding + popupXOffset, rowMax.Y),
            popupWidth,
            metrics.ActionButtonWidth,
            metrics.ItemSpacing,
            padding);
    }

    private static float ComputePopupScrollHeight()
    {
        var rowStep = ImGui.GetFrameHeightWithSpacing();
        var itemSpacingY = ImGui.GetStyle().ItemSpacing.Y;
        return Math.Max(rowStep, rowStep * PagePopupScrollAfterRows - itemSpacingY);
    }

    private static void PreparePopupBelow(string popupId, PopupLayout layout)
    {
        if (!ImGui.IsPopupOpen(popupId, ImGuiPopupFlags.None))
            return;

        ImGui.SetNextWindowPos(layout.Position, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(layout.TotalWidth, 0f), ImGuiCond.Always);
    }

    private static void PushPagePopupStyle(PopupLayout layout)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 0f);
        ImGui.PushStyleVar(
            ImGuiStyleVar.ItemSpacing,
            new Vector2(layout.ItemSpacing, layout.ItemSpacing));
    }

    private static void DrawPagePopup(
        ref int selectedPage,
        IList<PanelPage> pages,
        PopupLayout layout)
    {
        var useScroll = pages.Count > PagePopupScrollAfterRows;
        var scrollHeight = useScroll ? ComputePopupScrollHeight() : 0f;
        PreparePopupBelow(PagePopupId, layout);

        using (new PanelUiDropdownStyleScope(C.PanelUi))
        using (PanelUiTextStyle.PushText(C.PanelUi))
        {
            if (!ImGui.BeginPopup(
                    PagePopupId,
                    ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
                return;

            PushPagePopupStyle(layout);

            if (useScroll)
            {
                using var scroll = ImRaii.Child(
                    "##eqpPageSelectorScroll",
                    new Vector2(layout.TotalWidth, scrollHeight),
                    false);

                if (scroll)
                    DrawPagePopupItems(ref selectedPage, pages);
            }
            else
            {
                DrawPagePopupItems(ref selectedPage, pages);
            }

            ImGui.PopStyleVar(PagePopupStyleVarCount);
            ImGui.EndPopup();
        }
    }

    private static void DrawPagePopupItems(ref int selectedPage, IList<PanelPage> pages)
    {
        for (var page = 0; page < pages.Count; page++)
        {
            var itemLabel = GetDisplayName(pages[page], page);

            if (ImGui.Selectable(itemLabel, page == selectedPage))
                selectedPage = page;
        }
    }

    private static string GetDisplayName(PanelPage page, int pageIndex) =>
        string.IsNullOrWhiteSpace(page.Name)
            ? "（無題）"
            : page.Name.Trim();

    private static void HandleMouseWheel(ref int selectedPage, int pageCount)
    {
        var wheel = ImGui.GetIO().MouseWheel;
        if (wheel > 0f)
            selectedPage = Math.Max(0, selectedPage - 1);
        else if (wheel < 0f)
            selectedPage = Math.Min(pageCount - 1, selectedPage + 1);
    }
}

