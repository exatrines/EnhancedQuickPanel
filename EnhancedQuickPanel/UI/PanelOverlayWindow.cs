using System.Runtime.InteropServices;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using EnhancedQuickPanel.Models;
using EnhancedQuickPanel.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace EnhancedQuickPanel.UI;

/// <summary>Dalamud window that renders the quick panel overlay: slots, page bar, edit mode, dragging, and the right-click context menu.</summary>
public sealed class PanelOverlayWindow : Window
{
    private const float WindowBorderRounding = 5f;
    private const float EditColumnGap = 8f;
    private const string ContextMenuPopupId = "##eqpOverlayContext";
    private static readonly Vector2 CornerCountPositionOffset = new(3f, 4f);

    private const ImGuiWindowFlags ContextMenuWindowFlags =
        ImGuiWindowFlags.NoResize
        | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoTitleBar
        | ImGuiWindowFlags.NoSavedSettings;

    private const ImGuiWindowFlags OverlayFlags =
        ImGuiWindowFlags.NoDecoration
            | ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoNav
            | ImGuiWindowFlags.NoDocking
            | ImGuiWindowFlags.AlwaysAutoResize;

    private int _selectedPage;
    private int _selectedSlotIndex = -1;
    private int _selectionPage = -1;
    private bool _isEditingPageName;
    private bool _slotEditorExpanded;
    private ImRaii.ColorDisposable? _windowBgScope;
    private bool _isDraggingWindow;
    private Vector2 _dragMouseStart;
    private Vector2 _dragWindowStart;
    private Vector2? _contextMenuAnchor;

    public PanelOverlayWindow()
        : base("Enhanced Quick Panel##eqpOverlay", OverlayFlags, true)
    {
        IsOpen = true;
        RespectCloseHotkey = false;
    }

    public override bool DrawConditions() =>
        C.Enabled && GameModuleGuard.IsClientReady;

    public override void PreDraw()
    {
        ImGui.SetNextWindowPos(new Vector2(C.OverlayPosX, C.OverlayPosY), ImGuiCond.Always);
        var windowBg = _isEditingPageName ? C.EditModeWindowBgColor : C.WindowBgColor;
        _windowBgScope = ImRaii.PushColor(ImGuiCol.WindowBg, windowBg);
        var windowRounding = WindowBorderRounding;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, windowRounding);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(C.WindowPadding, C.WindowPadding));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(C.SlotPadding, C.SlotPadding));
    }

    public override void PostDraw()
    {
        _windowBgScope?.Dispose();
        _windowBgScope = null;
        ImGui.PopStyleVar(3);
        SlotIconPicker.Draw();
        NativeQuickPanelImportWindow.Draw();
    }

    public override void Draw() => GenericHelpers.Safe(DrawContent);

    private void DrawContent()
    {
        if (!GameModuleGuard.IsClientReady)
            return;

        if (_isEditingPageName)
            DrawEditingLayout();
        else
            DrawNormalLayout();

        C.EnsureDefaults();
        if (!_isEditingPageName)
            PageListWindow.Draw(ref _selectedPage);
        DrawWindowBorder();
        HandleWindowDrag();
        DrawOverlayContextMenu();

        if (_isEditingPageName)
            SyncEditDragSelection();

        SlotDragDropHandler.ProcessEndOfFrame();
        SlotSwapDragHandler.ProcessEndOfFrame();

        if (SlotSwapDragHandler.TryConsumeCompletedSwapTarget(out var swapPage, out var swapIndex))
        {
            _selectionPage = swapPage;
            _selectedSlotIndex = swapIndex;
        }
    }

    private void SyncEditDragSelection()
    {
        if (SlotSwapDragHandler.TryGetDragSource(out var page, out var index))
        {
            _selectionPage = page;
            _selectedSlotIndex = index;
        }
    }

    private void DrawOverlayContextMenu()
    {
        C.EnsureDefaults();
        if (!C.ContextMenuItems.HasVisibleItems)
            return;

        TryOpenOverlayContextMenu();

        var style = C.ContextMenu;
        var items = C.ContextMenuItems;
        style.EnsureDefaults();

        var padding = style.Padding;
        var editLabel = T("contextMenu.edit");
        var settingsLabel = T("contextMenu.settings");
        var exportLabel = T("contextMenu.exportPage");
        var importLabel = T("contextMenu.importPage");
        var importNativeLabel = T("contextMenu.importNative");
        var closeLabel = T("contextMenu.close");

        var visibleLabels = new List<string>(6);
        if (items.IsSettingsVisible)
            visibleLabels.Add(settingsLabel);
        if (items.IsImportPageVisible)
            visibleLabels.Add(importLabel);
        if (items.IsExportPageVisible)
            visibleLabels.Add(exportLabel);
        if (items.IsImportNativeVisible)
            visibleLabels.Add(importNativeLabel);
        if (items.IsEditVisible)
            visibleLabels.Add(editLabel);
        if (items.IsCloseVisible)
            visibleLabels.Add(closeLabel);

        if (visibleLabels.Count == 0)
            return;

        var labelSpan = CollectionsMarshal.AsSpan(visibleLabels);
        var menuWidth = ContextMenuItem.ComputeRequiredWidth(style, labelSpan);
        var rowHeight = ContextMenuItem.ComputeRowHeight(style, labelSpan);
        var menuItemCount = visibleLabels.Count;
        var windowHeight = rowHeight * menuItemCount + padding * 2f;
        if (_contextMenuAnchor is { } anchor)
            ImGui.SetNextWindowPos(anchor, ImGuiCond.Appearing, new Vector2(0f, 1f));

        ImGui.SetNextWindowSize(new Vector2(menuWidth, windowHeight), ImGuiCond.Always);

        using (new ContextMenuStyleScope(style))
        {
            if (!ImGui.BeginPopup(ContextMenuPopupId, ContextMenuWindowFlags))
                return;

            if (items.IsSettingsVisible
                && ContextMenuItem.Draw(settingsLabel, FontAwesomeIcon.Cog, "##eqpContextSettings", style))
                ToggleConfigWindow();

            if (items.IsImportPageVisible
                && ContextMenuItem.Draw(importLabel, FontAwesomeIcon.Download, "##eqpContextImportPage", style))
                ImportCurrentPage();

            if (items.IsExportPageVisible
                && ContextMenuItem.Draw(exportLabel, FontAwesomeIcon.Upload, "##eqpContextExportPage", style))
                ExportCurrentPage();

            if (items.IsImportNativeVisible
                && ContextMenuItem.Draw(
                    importNativeLabel,
                    FontAwesomeIcon.FileImport,
                    "##eqpContextImportNative",
                    style))
            {
                NativeQuickPanelImportWindow.Open(onImported: newPageIndex =>
                {
                    _selectedPage = newPageIndex;
                    if (_isEditingPageName)
                        SelectFirstSlotForEditMode();
                });
                ImGui.CloseCurrentPopup();
                _contextMenuAnchor = null;
            }

            if (items.IsEditVisible
                && ContextMenuItem.Draw(
                    editLabel,
                    FontAwesomeIcon.Pen,
                    "##eqpContextEdit",
                    style,
                    _isEditingPageName ? "✓" : null))
                ToggleEditMode();

            if (items.IsCloseVisible
                && ContextMenuItem.Draw(closeLabel, FontAwesomeIcon.Times, "##eqpContextClose", style))
                CloseOverlay();

            ImGui.EndPopup();
        }

        if (!ImGui.IsPopupOpen(ContextMenuPopupId))
            _contextMenuAnchor = null;
    }

    private void ExportCurrentPage()
    {
        C.EnsureDefaults();
        if (_selectedPage < 0 || _selectedPage >= C.Pages.Count)
        {
            Notify.Error(T("panelContent.error.noPage"));
            return;
        }

        PanelContentImportExport.ExportToClipboard(C.Pages[_selectedPage]);
    }

    private void ImportCurrentPage()
    {
        if (!PanelContentImportExport.TryImportFromClipboardAsNewPage(out var error))
        {
            Notify.Error(error);
            return;
        }

        _selectedPage = C.Pages.Count - 1;
        if (_isEditingPageName)
            SelectFirstSlotForEditMode();
    }

    private void ToggleEditMode()
    {
        var wasEditing = _isEditingPageName;
        _isEditingPageName = !_isEditingPageName;
        ApplyEditModeTransition(wasEditing);
    }

    private void ApplyEditModeTransition(bool wasEditing)
    {
        if (wasEditing && !_isEditingPageName)
        {
            C.OverlayPosX += ComputeEditModeLeftOffset();
            _slotEditorExpanded = false;
            _selectedSlotIndex = -1;
            _selectionPage = -1;
            PageListWindow.Close();
            SlotIconPicker.Close();
            EzConfig.Save();
            return;
        }

        if (!wasEditing && _isEditingPageName)
        {
            C.OverlayPosX -= ComputeEditModeLeftOffset();
            PageListWindow.Close();
            SelectFirstSlotForEditMode();
            EzConfig.Save();
        }
    }

    private static void ToggleConfigWindow()
    {
        if (EzConfigGui.Window == null)
        {
            EzConfigGui.Open();
            return;
        }

        EzConfigGui.Window.IsOpen = !EzConfigGui.Window.IsOpen;
    }

    private void TryOpenOverlayContextMenu()
    {
        if (!C.ContextMenuItems.HasVisibleItems)
            return;

        if (!ImGui.IsMouseReleased(ImGuiMouseButton.Right))
            return;

        if (!ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows))
            return;

        if (ImGui.IsPopupOpen(ContextMenuPopupId))
            return;

        _contextMenuAnchor = ImGui.GetIO().MousePos;
        ImGui.OpenPopup(ContextMenuPopupId);
    }

    public void ToggleVisibility()
    {
        C.EnsureDefaults();
        if (C.Enabled)
        {
            CloseOverlay();
            return;
        }

        C.Enabled = true;
        IsOpen = true;

        if (C.DisplayMode == QuickPanelDisplayMode.PluginOnly)
            QuickPanelAddon.HideNative();

        EzConfig.Save();
    }

    private void CloseOverlay()
    {
        if (_isEditingPageName)
        {
            C.OverlayPosX += ComputeEditModeLeftOffset();
            _isEditingPageName = false;
            _slotEditorExpanded = false;
            _selectedSlotIndex = -1;
            _selectionPage = -1;
        }

        PageListWindow.Close();
        SlotIconPicker.Close();
        C.Enabled = false;
        EzConfig.Save();
    }

    private void DrawWindowBorder()
    {
        var thickness = _isEditingPageName ? C.EditModeWindowBorderThickness : C.WindowBorderThickness;
        if (thickness <= 0f)
            return;

        var rounding = WindowBorderRounding;
        var color = ImGui.ColorConvertFloat4ToU32(
            _isEditingPageName ? C.EditModeWindowBorderColor : C.WindowBorderColor);
        var min = ImGui.GetWindowPos();
        var max = min + ImGui.GetWindowSize();
        var drawList = ImGui.GetWindowDrawList();
        var clipPad = thickness + 1f;
        var clipMin = min - new Vector2(clipPad, clipPad);
        var clipMax = max + new Vector2(clipPad, clipPad);
        drawList.PushClipRect(clipMin, clipMax, false);
        drawList.AddRect(
            min,
            max,
            color,
            rounding,
            ImDrawFlags.RoundCornersAll,
            thickness);
        drawList.PopClipRect();
    }

    private void HandleWindowDrag()
    {
        if (SlotDragDropHandler.IsGameDragActive
            || SlotSwapDragHandler.IsInternalDragActive
            || PageReorderDragHandler.IsDragging)
        {
            if (_isDraggingWindow && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                _isDraggingWindow = false;
                EzConfig.Save();
            }

            return;
        }

        var io = ImGui.GetIO();

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            if (_isDraggingWindow)
                EzConfig.Save();

            _isDraggingWindow = false;
        }

        if (_isDraggingWindow)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                var delta = io.MousePos - _dragMouseStart;
                C.OverlayPosX = _dragWindowStart.X + delta.X;
                C.OverlayPosY = _dragWindowStart.Y + delta.Y;
            }

            return;
        }

        if (!ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows))
            return;

        if (ImGui.IsAnyItemHovered() || ImGui.IsAnyItemActive())
            return;

        if (ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId))
            return;

        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            return;

        _isDraggingWindow = true;
        _dragMouseStart = io.MousePos;
        _dragWindowStart = new Vector2(C.OverlayPosX, C.OverlayPosY);
    }

    private void DrawNormalLayout()
    {
        DrawLayoutWithPageBar(isEditMode: false);
    }

    private void DrawEditingLayout()
    {
        var mainSize = ComputeMainContentSize();
        var columnWidth = mainSize.X;
        var columnHeight = mainSize.Y;

        ImGui.BeginGroup();
        if (DrawPageListPanel(columnWidth, columnHeight))
            OnCurrentPageRemoved();
        ImGui.EndGroup();

        ImGui.SameLine(0f, EditColumnGap);
        DrawVerticalSeparator(columnHeight);
        ImGui.SameLine(0f, EditColumnGap);

        DrawMainContentGroup(isEditMode: true);

        ImGui.SameLine(0f, EditColumnGap);
        DrawVerticalSeparator(columnHeight);
        ImGui.SameLine(0f, EditColumnGap);

        ImGui.BeginGroup();
        DrawSlotEditorPanel(columnWidth * (_slotEditorExpanded ? 2f : 1f), columnHeight);
        ImGui.EndGroup();
    }

    private void OnCurrentPageRemoved()
    {
        _selectedSlotIndex = -1;
        _selectionPage = -1;
        SelectFirstSlotForEditMode();
    }

    private void DrawMainContentGroup(bool isEditMode)
    {
        ImGui.BeginGroup();
        DrawLayoutWithPageBar(isEditMode);
        ImGui.EndGroup();
    }

    private static Vector2 ComputeMainContentSize()
    {
        var gridSpan = Configuration.GridSize * C.SlotSize
            + Math.Max(0, Configuration.GridSize - 1) * C.SlotPadding;
        var pageBarHeight = ImGui.GetFrameHeight();
        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        var height = gridSpan + spacing + pageBarHeight;

        return new Vector2(gridSpan, height);
    }

    private static float ComputeEditModeLeftOffset()
    {
        const float separatorWidth = 1f;
        var columnWidth = ComputeMainContentSize().X;
        return columnWidth + EditColumnGap + separatorWidth + EditColumnGap;
    }

    private static void DrawVerticalSeparator(float height)
    {
        var separatorHeight = Math.Max(0f, height);
        var topY = ImGui.GetCursorScreenPos().Y;
        var bottomY = topY + separatorHeight;
        var x = ImGui.GetCursorScreenPos().X;
        ImGui.GetWindowDrawList().AddLine(
            new Vector2(x, topY),
            new Vector2(x, bottomY),
            ImGui.GetColorU32(ImGuiCol.Separator));
        ImGui.Dummy(new Vector2(1f, separatorHeight));
    }

    private bool DrawPageListPanel(float width, float height)
    {
        using var child = ImRaii.Child("##eqpOverlayPageList", new Vector2(width, height), false);
        if (!child)
            return false;

        using var textScope = PanelUiTextStyle.PushText(C.PanelUi);
        var pageRemoved = PageListPanel.Draw(ref _selectedPage, C.Pages, C.PanelUi, width, height);
        PageReorderDragHandler.ProcessEndOfFrame(C.Pages, ref _selectedPage);
        return pageRemoved;
    }

    private void DrawSlotEditorPanel(float width, float height)
    {
        if (_selectedSlotIndex < 0)
        {
            ImGui.Dummy(new Vector2(width, height));
            return;
        }

        using var child = ImRaii.Child("##eqpOverlaySlotEditor", new Vector2(width, height), false);
        if (!child)
            return;

        using var textScope = PanelUiTextStyle.PushText(C.PanelUi);
        SlotEditor.Draw(
            C.Pages[_selectedPage].Slots[_selectedSlotIndex],
            overlayPanel: true,
            ref _slotEditorExpanded);
    }

    private void DrawLayoutWithPageBar(bool isEditMode)
    {
        DrawPageTabs();
        DrawGrid(isEditMode);
    }

    private void DrawPageTabs(bool separatorBefore = false, bool separatorAfter = false)
    {
        var barWidth = Configuration.GridSize * C.SlotSize
            + Math.Max(0, Configuration.GridSize - 1) * C.SlotPadding;
        var wasEditing = _isEditingPageName;
        PageSelectorBar.Draw(
            ref _selectedPage,
            C.Pages,
            ref _isEditingPageName,
            C.PanelUi,
            barWidth,
            showSeparatorBefore: separatorBefore,
            showSeparatorAfter: separatorAfter,
            showAddPageButton: _isEditingPageName,
            showPenButton: C.ShowEditButton,
            pagePopupXOffset: ComputeEditModeLeftOffset());

        if (wasEditing && !_isEditingPageName)
            ApplyEditModeTransition(wasEditing);
        else if (!wasEditing && _isEditingPageName)
            ApplyEditModeTransition(wasEditing);

        if (_isEditingPageName && _selectionPage != _selectedPage)
            SelectFirstSlotForEditMode();
    }

    private void SelectFirstSlotForEditMode()
    {
        _selectedSlotIndex = 0;
        _selectionPage = _selectedPage;
    }

    private void DrawGrid(bool isEditMode)
    {
        C.EnsureDefaults();

        if (isEditMode)
            SlotSwapDragHandler.BeginFrame();

        for (var row = 0; row < Configuration.GridSize; row++)
        {
            for (var col = 0; col < Configuration.GridSize; col++)
            {
                if (col > 0)
                    ImGui.SameLine();

                var index = row * Configuration.GridSize + col;
                DrawSlot(
                    C.Pages[_selectedPage].Slots[index],
                    _selectedPage,
                    index,
                    isEditMode,
                    isEditMode && _selectionPage == _selectedPage && (
                        _selectedSlotIndex == index
                        || SlotSwapDragHandler.IsSourceSlot(_selectedPage, index)));
            }
        }
    }

    private void DrawSlot(
        PanelSlot slot,
        int page,
        int index,
        bool isEditMode,
        bool isSelected)
    {
        var icon = SlotIconResolver.ResolveIcon(slot);
        var runtime = slot.Kind == PanelSlotKind.Action
            ? SlotRuntimeCache.Get(slot, icon)
            : SlotRuntimeState.Default;
        var overlay = slot.Kind switch
        {
            PanelSlotKind.Macro => new SlotOverlayInfo(false, 0, false, true),
            PanelSlotKind.Action => SlotOverlayResolver.Resolve(slot, icon, runtime),
            _ => SlotOverlayInfo.None,
        };
        var cooldown = !isEditMode && slot.IsConfigured && slot.Kind == PanelSlotKind.Action
            ? runtime.Cooldown
            : SlotCooldownInfo.None;
        RaptureHotbarModule.HotbarSlotType? slotType = slot.Kind == PanelSlotKind.Action
            ? (RaptureHotbarModule.HotbarSlotType)slot.CommandType
            : null;

        DrawSlotButton(
            slot,
            icon,
            overlay,
            cooldown,
            page,
            index,
            slotType,
            isEditMode,
            isSelected,
            () =>
            {
                if (!isEditMode && slot.IsConfigured)
                    SlotExecutor.Execute(slot);
            },
            () =>
            {
                _selectedSlotIndex = index;
                _selectionPage = page;
            });
    }

    private static void DrawSlotButton(
        PanelSlot slot,
        ResolvedSlotIcon icon,
        SlotOverlayInfo overlay,
        SlotCooldownInfo cooldown,
        int page,
        int index,
        RaptureHotbarModule.HotbarSlotType? slotType,
        bool isEditMode,
        bool isSelected,
        Action onClick,
        Action onSelect)
    {
        var size = new Vector2(C.SlotSize, C.SlotSize);

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.PushID(index);
        var topLeft = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("##eqpIcon", size);
        ImGui.PopID();

        var hovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
        var clickedIn = isEditMode && hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        var gameDropClick = false;

        if (isEditMode)
        {
            if (clickedIn && !SlotDragDropHandler.IsGameDragActive)
                onSelect();

            gameDropClick = SlotDragDropHandler.TryHandleDropOnClick(page, index, slot);
            if (gameDropClick)
                onSelect();

            SlotSwapDragHandler.TryBeginDragSource(page, index, enabled: true);
            SlotSwapDragHandler.TryAcceptSwapTarget(page, index);
            SlotDragDropHandler.NotifySlotHover(page, index, hovered);
        }

        var showSelected = isSelected || clickedIn;

        var drawList = ImGui.GetWindowDrawList();
        var isGrayedOut = overlay.IsGrayedOut || cooldown.IsActive;
        var iconTint = isGrayedOut
            ? ImGui.ColorConvertFloat4ToU32(new Vector4(0.5f, 0.5f, 0.5f, 1f))
            : uint.MaxValue;

        var isDropTarget = isEditMode
            && (SlotDragDropHandler.ShouldHighlightDropTarget(page, index)
                || SlotSwapDragHandler.ShouldHighlightSwapTarget(page, index));

        var showEmptyBorder = C.ShowEmptySlotBorder ?? true;
        var drawBaseFrame = isEditMode || slot.IsConfigured || showEmptyBorder;
        if (drawBaseFrame)
            SlotBackgroundResolver.DrawBaseFrame(drawList, topLeft, topLeft + size, isGrayedOut);

        var drewIcon = TryDrawSlotIcon(drawList, topLeft, size, icon, slotType, iconTint);
        if (drewIcon)
            IconFrameResolver.DrawIconFrame(drawList, topLeft, topLeft + size, isGrayedOut);

        if (drewIcon && cooldown.IsActive)
            SlotCooldownRenderer.Draw(drawList, topLeft, topLeft + size, cooldown);

        if (drewIcon && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            IconFrameResolver.DrawHoverFrame(drawList, topLeft, topLeft + size, isGrayedOut: isGrayedOut);

        if (showSelected)
        {
            var selectionColor = ImGui.ColorConvertFloat4ToU32(C.SelectedSlotBorderColor);
            drawList.AddRect(topLeft, topLeft + size, selectionColor, 4f, ImDrawFlags.None, 2f);
        }

        DrawSlotOverlays(overlay, isGrayedOut, cooldown);

        if (isDropTarget)
            SlotBackgroundResolver.DrawDropTargetOverlay(drawList, topLeft, topLeft + size, isGrayedOut);

        ImGui.PopStyleVar();

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            var tooltip = SlotIconResolver.ResolveTooltip(slot);
            if (!string.IsNullOrWhiteSpace(tooltip))
                DrawSlotTooltip(tooltip, topLeft, size);
        }

        if (gameDropClick)
            return;

        if (isEditMode)
        {
            if (SlotDragDropHandler.TryActivateSlot(page, index))
                onSelect();
            return;
        }

        if (SlotDragDropHandler.TryActivateSlot(page, index))
            onClick();
    }

    private static void DrawSlotTooltip(string tooltip, Vector2 slotTopLeft, Vector2 slotSize)
    {
        const float gapPixels = 12f;
        var anchor = slotTopLeft + new Vector2(slotSize.X * 0.5f, -gapPixels);
        ImGui.SetNextWindowPos(anchor, ImGuiCond.Always, new Vector2(0.5f, 1f));

        using (ImRaii.PushColor(ImGuiCol.PopupBg, C.TooltipBgColor))
        using (ImRaii.PushColor(ImGuiCol.Text, C.TooltipTextColor))
        {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8f);
            ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 8f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(C.TooltipPadding, C.TooltipPadding));
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35f);
            ImGui.TextUnformatted(tooltip);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
            ImGui.PopStyleVar(3);
        }
    }

    private static bool TryDrawSlotIcon(
        ImDrawListPtr drawList,
        Vector2 topLeft,
        Vector2 size,
        ResolvedSlotIcon icon,
        RaptureHotbarModule.HotbarSlotType? slotType,
        uint iconTint)
    {
        if (!icon.IsValid)
            return false;

        if (SlotTextureResolver.TryGetSlotTexture(icon, out var texture))
            return SafeTextureDraw.TryAddImage(drawList, texture, topLeft, topLeft + size, iconTint);

        return false;
    }

    private static void DrawSlotOverlays(SlotOverlayInfo overlay, bool isGrayedOut, SlotCooldownInfo cooldown)
    {
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var drawList = ImGui.GetWindowDrawList();

        if (overlay.ShowMacroIndicator)
            DrawMacroIndicator(drawList, min, max, isGrayedOut);

        if (overlay.ShowActionCharges)
            DrawActionChargeText(drawList, min, max, overlay.ActionCharges, overlay.IsGrayedOut);
        else if (overlay.ShowQuantity && !cooldown.IsActive)
            DrawQuantityText(drawList, min, max, overlay.Quantity, isGrayedOut);
    }

    private static void DrawMacroIndicator(
        ImDrawListPtr drawList,
        Vector2 slotMin,
        Vector2 slotMax,
        bool isGrayedOut)
    {
        var iconText = FontAwesomeIcon.Cog.ToIconString();
        var iconFont = UiBuilder.IconFont;
        var fontSize = SlotOverlayFontSizeResolver.ResolveFontSize(C.SlotSize, C.MacroGearLabelStyle);
        Vector2 textSize;
        using (ImRaii.PushFont(iconFont))
            textSize = ImGui.CalcTextSize(iconText) * (fontSize / ImGui.GetFontSize());
        var pos = new Vector2(slotMax.X - textSize.X + 5f, slotMin.Y - 4f);

        SlotOverlayTextRenderer.Draw(
            drawList,
            iconFont,
            fontSize,
            pos,
            iconText,
            C.MacroGearLabelStyle,
            OverlayLabelLetterSpacing.Default,
            isGrayedOut);
    }

    private static void DrawQuantityText(
        ImDrawListPtr drawList,
        Vector2 slotMin,
        Vector2 slotMax,
        int quantity,
        bool isGrayedOut) =>
        DrawCornerCountText(
            drawList,
            slotMin,
            slotMax,
            $"x{quantity}",
            C.QuantityLabelStyle,
            OverlayLabelLetterSpacing.Quantity,
            isGrayedOut,
            CornerCountPositionOffset);

    private static void DrawActionChargeText(
        ImDrawListPtr drawList,
        Vector2 slotMin,
        Vector2 slotMax,
        int charges,
        bool isGrayedOut) =>
        DrawCornerCountText(
            drawList,
            slotMin,
            slotMax,
            charges.ToString(),
            C.ChargeLabelStyle,
            OverlayLabelLetterSpacing.Default,
            isGrayedOut,
            new Vector2(1f, 2f));

    private static void DrawCornerCountText(
        ImDrawListPtr drawList,
        Vector2 slotMin,
        Vector2 slotMax,
        string text,
        OverlayLabelStyleConfig style,
        float letterSpacing,
        bool isGrayedOut,
        Vector2 positionOffset = default)
    {
        var fontSize = SlotOverlayFontSizeResolver.ResolveFontSize(C.SlotSize, style);
        var textSize = SlotOverlayTextRenderer.MeasureTextSize(text, ImGui.GetFont(), fontSize, letterSpacing);
        var pos = new Vector2(slotMax.X - textSize.X - 2f, slotMax.Y - textSize.Y - 1f) + positionOffset;

        SlotOverlayTextRenderer.Draw(
            drawList,
            ImGui.GetFont(),
            fontSize,
            pos,
            text,
            style,
            letterSpacing,
            isGrayedOut);
    }
}
