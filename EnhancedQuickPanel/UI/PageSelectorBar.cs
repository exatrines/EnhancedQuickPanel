using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using EnhancedQuickPanel.Models;

namespace EnhancedQuickPanel.UI;

/// <summary>Draws the overlay's page-name bar and handles page switching via the click popup and mouse wheel.</summary>
internal static class PageSelectorBar
{
    private const string PagePopupId = "##eqpPageSelectorPopup";

    private const int PagePopupStyleVarCount = 3;
    private const int PagePopupScrollAfterRows = 5;

    private static bool _wheelApplied;

    internal static bool IsPagePopupOpen() => ImGui.IsPopupOpen(PagePopupId);

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
        float ItemSpacing);

    public static void Draw(
        ref int selectedPage,
        IList<PanelPage> pages,
        ref bool isEditingPageName,
        PanelUiStyleConfig style,
        float? barWidth = null,
        bool showPenButton = true,
        bool showCollapseButton = false,
        bool showPageSelector = true,
        float pagePopupXOffset = 0f,
        Action? onCollapse = null)
    {
        Config.EnsureDefaults();
        if (pages.Count == 0)
            return;

        selectedPage = Math.Clamp(selectedPage, 0, pages.Count - 1);

        var metrics = CreateMetrics(barWidth, showPenButton, showCollapseButton, showPageSelector);

        using (new PanelUiButtonStyleScope(style))
            DrawSelectorRow(
                ref selectedPage,
                pages,
                ref isEditingPageName,
                style,
                metrics,
                showPenButton,
                showCollapseButton,
                showPageSelector,
                onCollapse);

        if (showPageSelector)
            DrawPagePopup(
                ref selectedPage,
                pages,
                CreatePopupLayout(metrics, isEditingPageName ? pagePopupXOffset : 0f));
    }

    private static PageBarMetrics CreateMetrics(
        float? barWidth,
        bool showPenButton,
        bool showCollapseButton,
        bool showPageSelector)
    {
        var actionButtonWidth = ImGui.GetFrameHeight();
        var rowGap = ImGui.GetStyle().ItemSpacing.X;
        var iconButtonCount = (showCollapseButton ? 1 : 0) + (showPenButton ? 1 : 0);
        var gapCount = iconButtonCount;

        if (!barWidth.HasValue)
            return new PageBarMetrics(null, showPageSelector ? null : 0f, actionButtonWidth, rowGap);

        var selectorWidth = Math.Max(
            showPageSelector ? 64f : 0f,
            barWidth.Value - actionButtonWidth * iconButtonCount - rowGap * gapCount);
        return new PageBarMetrics(barWidth.Value, selectorWidth, actionButtonWidth, rowGap);
    }

    private static void DrawSelectorRow(
        ref int selectedPage,
        IList<PanelPage> pages,
        ref bool isEditingPageName,
        PanelUiStyleConfig style,
        PageBarMetrics metrics,
        bool showPenButton,
        bool showCollapseButton,
        bool showPageSelector,
        Action? onCollapse)
    {
        var rowHeight = ImGui.GetFrameHeight();
        var drewItem = false;

        if (showCollapseButton)
        {
            DrawCollapseButton(style, metrics.ActionButtonWidth, onCollapse);
            drewItem = true;
        }

        if (showPageSelector)
        {
            if (drewItem)
                ImGui.SameLine(0, metrics.ItemSpacing);

            var selectorSize = metrics.SelectorWidth.HasValue
                ? new Vector2(metrics.SelectorWidth.Value, rowHeight)
                : new Vector2(0f, rowHeight);
            DrawPageLabel(ref selectedPage, pages, selectorSize);
            drewItem = true;
        }
        else if (showPenButton && metrics.SelectorWidth is > 0f)
        {
            if (drewItem)
                ImGui.SameLine(0, metrics.ItemSpacing);

            ImGui.Dummy(new Vector2(metrics.SelectorWidth.Value, rowHeight));
            drewItem = true;
        }

        if (!showPenButton)
            return;

        if (drewItem)
            ImGui.SameLine(0, metrics.ItemSpacing);
        DrawPenButton(ref isEditingPageName, style, metrics.ActionButtonWidth, rowHeight);
    }

    private static void DrawCollapseButton(PanelUiStyleConfig style, float actionButtonWidth, Action? onCollapse)
    {
        var size = new Vector2(actionButtonWidth, actionButtonWidth);
        var clicked = CenteredIconButton.Draw(
                PanelCollapse.Icon,
                "##eqpCollapse",
                size,
                style.TextColor,
                style.TextHoverColor);
        if (PanelOverlayWindow.ConsumeClickWithoutDrag(clicked))
            onCollapse?.Invoke();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(PanelCollapse.Label);
    }

    private static void DrawPageLabel(
        ref int selectedPage,
        IList<PanelPage> pages,
        Vector2 size)
    {
        var displayName = pages[selectedPage].DisplayName;

        var clicked = ImGui.Button($"{displayName}##eqpPageSelector", size);
        if (PanelOverlayWindow.ConsumeClickWithoutDrag(clicked) && (Config.ShowPageSelectorPopup ?? true))
            ImGui.OpenPopup(PagePopupId);

        if (ImGui.IsItemHovered() && Config.SwitchPageOnPageNameWheel)
            TryApplyMouseWheel(ref selectedPage, pages.Count);
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
            var clicked = CenteredIconButton.Draw(
                    FontAwesomeIcon.Pen,
                    "##eqpEditPage",
                    size,
                    style.TextColor,
                    style.TextHoverColor);
            if (PanelOverlayWindow.ConsumeClickWithoutDrag(clicked))
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
        var padding = Config.WindowPadding;
        var windowPos = ImGui.GetWindowPos();
        var popupWidth = metrics.TotalWidth
            ?? Math.Max(64f, ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X);

        return new PopupLayout(
            new Vector2(windowPos.X + padding + popupXOffset, rowMax.Y),
            popupWidth,
            metrics.ItemSpacing);
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

        using (new PanelUiDropdownStyleScope(Config.PanelUi))
        using (PanelUiTextStyle.PushText(Config.PanelUi))
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
            var itemLabel = pages[page].DisplayName;

            if (ImGui.Selectable(itemLabel, page == selectedPage))
                selectedPage = page;
        }
    }

    internal static void TryApplyMouseWheel(ref int selectedPage, int pageCount)
    {
        if (_wheelApplied)
            return;

        var wheel = ImGui.GetIO().MouseWheel;
        if (wheel > 0f)
            selectedPage = Math.Max(0, selectedPage - 1);
        else if (wheel < 0f)
            selectedPage = Math.Min(pageCount - 1, selectedPage + 1);
        else
            return;

        _wheelApplied = true;
    }

    internal static void BeginFrame() => _wheelApplied = false;
}

