using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using EnhancedQuickPanel.Models;
using EnhancedQuickPanel.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace EnhancedQuickPanel.UI;

// Panel window: slots, page bar, edit mode, dragging, and the right-click context menu.
public sealed class PanelOverlayWindow : Window
{
    private const float WindowBorderRounding = 5f;
    private const float EditColumnGap = 8f;
    private static readonly Vector2 CornerCountPositionOffset = new(3f, 4f);

    private const ImGuiWindowFlags PanelWindowFlags =
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
    private bool _drawing;
    private bool _pendingCommit;
    private bool _editingAtDrawStart;
    private ImRaii.ColorDisposable? _windowBgScope;
    private bool _isDraggingWindow;
    private Vector2 _dragMouseStart;
    private Vector2 _dragWindowStart;
    private static bool _chromeDragRequested;
    private static Vector2 _pressPos;
    private static bool _pressMoved;
    private static bool _pluginRightClickConsumed;
    private static bool _slotContextClickedThisFrame;
    private static PanelSlot? _rightPressedSlot;
    private static PanelSlot? _middlePressedSlot;

    public PanelOverlayWindow()
        : base("Enhanced Quick Panel##eqpPanel", PanelWindowFlags, true)
    {
        IsOpen = true;
        RespectCloseHotkey = false;
    }

    public override bool DrawConditions() =>
        Config.Enabled && GameModuleGuard.IsClientReady;

    public override void PreDraw()
    {
        ImGui.SetNextWindowPos(new Vector2(Config.OverlayPosX, Config.OverlayPosY), ImGuiCond.Always);
        var windowBg = _isEditingPageName ? Config.EditModeWindowBgColor : Config.WindowBgColor;
        _windowBgScope = ImRaii.PushColor(ImGuiCol.WindowBg, windowBg);
        var windowRounding = WindowBorderRounding;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, windowRounding);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(Config.WindowPadding, Config.WindowPadding));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(Config.SlotPadding, Config.SlotPadding));
    }

    public override void PostDraw()
    {
        _windowBgScope?.Dispose();
        _windowBgScope = null;
        ImGui.PopStyleVar(3);
        SlotIconPicker.Draw();
        NativeQuickPanelImportPopup.Draw();
    }

    public override void Draw() => AddonAccess.Safe(DrawContent);

    private void DrawContent()
    {
        _pluginRightClickConsumed = false;
        _slotContextClickedThisFrame = false;
        if (!GameModuleGuard.IsClientReady)
            return;

        Config.EnsureDefaults();
        if (_selectedSlotIndex >= Config.SlotsPerPage)
            _selectedSlotIndex = 0;

        _drawing = true;
        _editingAtDrawStart = _isEditingPageName;
        try
        {
            if (_isEditingPageName)
                DrawEditingLayout();
            else if (Config.IsCollapsed)
                DrawPageTabs();
            else
                DrawNormalLayout();

            DrawWindowBorder();
            HandleWindowDrag();
            PanelContextMenu.Draw(
                _pluginRightClickConsumed,
                _slotContextClickedThisFrame,
                _isEditingPageName,
                ImportCurrentPage,
                ExportCurrentPage,
                OnNativePageImported,
                ToggleEditMode,
                ToggleCollapse,
                CloseOverlay,
                EditSlotAt);

            if (!ImGui.IsMouseDown(ImGuiMouseButton.Right)) _rightPressedSlot = null;
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Middle)) _middlePressedSlot = null;

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
        finally
        {
            try
            {
                if (_pendingCommit)
                    CommitPanelState(_editingAtDrawStart);
            }
            finally
            {
                _drawing = false;
            }
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

    private void OnNativePageImported(int newPageIndex)
    {
        _selectedPage = newPageIndex;
        if (_isEditingPageName)
            SelectFirstSlotForEditMode();
    }

    private void ExportCurrentPage()
    {
        Config.EnsureDefaults();
        if (_selectedPage < 0 || _selectedPage >= Config.Pages.Count)
        {
            Notifications.Error(T("panelContent.error.noPage"));
            return;
        }

        PanelContentImportExport.ExportToClipboard(Config.Pages[_selectedPage]);
    }

    private void ImportCurrentPage()
    {
        if (!PanelContentImportExport.TryImportFromClipboardAsNewPage(out var error))
        {
            Notifications.Error(error);
            return;
        }

        _selectedPage = Config.Pages.Count - 1;
        if (_isEditingPageName)
            SelectFirstSlotForEditMode();
    }

    private void ToggleEditMode()
    {
        if (Config.IsCollapsed && !_isEditingPageName)
            Config.IsCollapsed = false;

        _isEditingPageName = !_isEditingPageName;
        RequestCommit(_editingAtDrawStart);
    }

    private void ToggleCollapse()
    {
        if (Config.IsCollapsed)
        {
            Config.IsCollapsed = false;
            RequestCommit(_editingAtDrawStart);
            return;
        }

        if (_isEditingPageName)
            _isEditingPageName = false;

        Config.IsCollapsed = true;
        RequestCommit(_editingAtDrawStart);
    }

    private void RequestCommit(bool wasEditing)
    {
        _pendingCommit = true;
        if (!_drawing)
            CommitPanelState(wasEditing);
    }

    private void CommitPanelState(bool wasEditing)
    {
        if (wasEditing && !_isEditingPageName)
        {
            Config.OverlayPosX += ComputeEditModeLeftOffset();
            _slotEditorExpanded = false;
            _selectedSlotIndex = -1;
            _selectionPage = -1;
            SlotIconPicker.Close();
        }
        else if (!wasEditing && _isEditingPageName)
        {
            Config.OverlayPosX -= ComputeEditModeLeftOffset();
            if (!HasValidEditSelection())
                SelectFirstSlotForEditMode();
        }

        _pendingCommit = false;
        Config.Save();
    }

    private bool HasValidEditSelection()
    {
        if (_selectionPage < 0 || _selectionPage >= Config.Pages.Count)
            return false;
        var slots = Config.Pages[_selectionPage].Slots;
        return _selectedSlotIndex >= 0 && _selectedSlotIndex < slots.Count;
    }

    private void EditSlotAt(int page, int index)
    {
        _selectedPage = page;
        _selectedSlotIndex = index;
        _selectionPage = page;
        if (!_isEditingPageName)
        {
            if (Config.IsCollapsed)
                Config.IsCollapsed = false;
            _isEditingPageName = true;
        }

        RequestCommit(_editingAtDrawStart);
    }

    public void ToggleVisibility()
    {
        Config.EnsureDefaults();
        if (Config.Enabled)
        {
            CloseOverlay();
            return;
        }

        Config.Enabled = true;
        IsOpen = true;

        if (Config.DisplayMode == PanelDisplayMode.PluginOnly)
            NativeQuickPanelAddon.HideNative();

        Config.Save();
    }

    private void CloseOverlay()
    {
        var wasEditing = _drawing ? _editingAtDrawStart : _isEditingPageName;
        if (_isEditingPageName)
            _isEditingPageName = false;

        SlotIconPicker.Close();
        Config.Enabled = false;
        RequestCommit(wasEditing);
    }

    private void DrawWindowBorder()
    {
        var thickness = _isEditingPageName ? Config.EditModeWindowBorderThickness : Config.WindowBorderThickness;
        if (thickness <= 0f)
            return;

        var rounding = WindowBorderRounding;
        var color = ImGui.ColorConvertFloat4ToU32(
            _isEditingPageName ? Config.EditModeWindowBorderColor : Config.WindowBorderColor);
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
            _chromeDragRequested = false;
            if (_isDraggingWindow && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                _isDraggingWindow = false;
                Config.Save();
            }

            return;
        }

        var io = ImGui.GetIO();

        if (_chromeDragRequested)
        {
            _chromeDragRequested = false;
            if (!_isDraggingWindow && ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                _isDraggingWindow = true;
                _dragMouseStart = io.MousePos;
                _dragWindowStart = new Vector2(Config.OverlayPosX, Config.OverlayPosY);
            }
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            if (_isDraggingWindow)
                Config.Save();

            _isDraggingWindow = false;
        }

        if (_isDraggingWindow)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                var delta = io.MousePos - _dragMouseStart;
                Config.OverlayPosX = _dragWindowStart.X + delta.X;
                Config.OverlayPosY = _dragWindowStart.Y + delta.Y;
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
        _dragWindowStart = new Vector2(Config.OverlayPosX, Config.OverlayPosY);
    }

    internal static void RequestChromeDrag() => _chromeDragRequested = true;

    internal static bool ConsumeClickWithoutDrag(bool imguiClicked)
    {
        var io = ImGui.GetIO();
        if (ImGui.IsItemActivated())
        {
            _pressPos = io.MousePos;
            _pressMoved = false;
        }

        if (ImGui.IsItemActive() && !_pressMoved)
        {
            var delta = io.MousePos - _pressPos;
            var threshold = Math.Max(4f, io.MouseDragThreshold);
            if (delta.LengthSquared() >= threshold * threshold)
            {
                _pressMoved = true;
                RequestChromeDrag();
            }
        }

        return imguiClicked && !_pressMoved;
    }

    private void DrawNormalLayout()
    {
        DrawLayoutWithPageBar(isEditMode: false);
    }

    private void DrawEditingLayout()
    {
        var mainSize = ComputeMainContentSize();
        var sideSize = ComputeSidePanelSize();
        var separatorHeight = Math.Max(mainSize.Y, sideSize.Y);

        ImGui.BeginGroup();
        if (DrawPageListPanel(sideSize.X, sideSize.Y))
            OnCurrentPageRemoved();
        ImGui.EndGroup();

        ImGui.SameLine(0f, EditColumnGap);
        DrawVerticalSeparator(separatorHeight);
        ImGui.SameLine(0f, EditColumnGap);

        DrawMainContentGroup(isEditMode: true);

        ImGui.SameLine(0f, EditColumnGap);
        DrawVerticalSeparator(separatorHeight);
        ImGui.SameLine(0f, EditColumnGap);

        ImGui.BeginGroup();
        DrawSlotEditorPanel(sideSize.X * (_slotEditorExpanded ? 2f : 1f), sideSize.Y);
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
        var gridWidth = Config.ComputeGridWidth();
        var gridHeight = Config.ComputeGridHeight();
        var pageBarHeight = ImGui.GetFrameHeight();
        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        return new Vector2(gridWidth, gridHeight + spacing + pageBarHeight);
    }

    private static Vector2 ComputeSidePanelSize()
    {
        var span = PanelLayout.ComputeSpan(PanelLayout.BlockSize, Config.SlotSize, Config.SlotPadding);
        var pageBarHeight = ImGui.GetFrameHeight();
        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        return new Vector2(span, span + spacing + pageBarHeight);
    }

    private static float ComputeEditModeLeftOffset()
    {
        const float separatorWidth = 1f;
        var columnWidth = ComputeSidePanelSize().X;
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
        using var child = ImRaii.Child("##eqpPageList", new Vector2(width, height), false);
        if (!child)
            return false;

        using var textScope = PanelUiTextStyle.PushText(Config.PanelUi);
        var pageRemoved = PageListPanel.Draw(ref _selectedPage, Config.Pages, Config.PanelUi, width, height);
        PageReorderDragHandler.ProcessEndOfFrame(Config.Pages, ref _selectedPage);
        return pageRemoved;
    }

    private void DrawSlotEditorPanel(float width, float height)
    {
        if (_selectedSlotIndex < 0)
        {
            ImGui.Dummy(new Vector2(width, height));
            return;
        }

        using var child = ImRaii.Child("##eqpSlotEditor", new Vector2(width, height), false);
        if (!child)
            return;

        using var textScope = PanelUiTextStyle.PushText(Config.PanelUi);
        SlotEditor.Draw(
            Config.Pages[_selectedPage].Slots[_selectedSlotIndex],
            ref _slotEditorExpanded);
    }

    private void DrawLayoutWithPageBar(bool isEditMode)
    {
        DrawPageTabs();
        DrawGrid(isEditMode);
    }

    private void DrawPageTabs(bool separatorBefore = false, bool separatorAfter = false)
    {
        var barWidth = Config.ComputeGridWidth();
        var collapsed = !_isEditingPageName && Config.IsCollapsed;
        var wasEditing = _isEditingPageName;
        PageSelectorBar.Draw(
            ref _selectedPage,
            Config.Pages,
            ref _isEditingPageName,
            Config.PanelUi,
            collapsed ? null : barWidth,
            showSeparatorBefore: separatorBefore,
            showSeparatorAfter: separatorAfter,
            showPenButton: Config.ShowEditButton && !collapsed,
            showCollapseButton: collapsed || Config.ShowCollapseButton,
            showPageSelector: !collapsed,
            pagePopupXOffset: ComputeEditModeLeftOffset(),
            onCollapse: ToggleCollapse);

        if (wasEditing != _isEditingPageName)
            _pendingCommit = true;

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
        Config.EnsureDefaults();

        if (isEditMode)
            SlotSwapDragHandler.BeginFrame();

        var columns = Config.GridColumns;
        var rows = Config.GridRows;
        var slots = Config.Pages[_selectedPage].Slots;

        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < columns; col++)
            {
                if (col > 0)
                    ImGui.SameLine();

                var index = row * columns + col;
                var slot = index < slots.Count ? slots[index] : new PanelSlot();
                DrawSlot(
                    slot,
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
        var size = new Vector2(Config.SlotSize, Config.SlotSize);

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.PushID(index);
        var topLeft = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton("##eqpIcon", size);
        var clickWithoutDrag = !isEditMode && ConsumeClickWithoutDrag(clicked);
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
        var pluginVisual = PluginShortcuts.ResolveVisual(slot);
        var isGrayedOut = overlay.IsGrayedOut || cooldown.IsActive
            || PluginShortcuts.IsDimmed(pluginVisual);
        var iconTint = isGrayedOut
            ? ImGui.ColorConvertFloat4ToU32(new Vector4(0.5f, 0.5f, 0.5f, 1f))
            : uint.MaxValue;

        var isDropTarget = isEditMode
            && (SlotDragDropHandler.ShouldHighlightDropTarget(page, index)
                || SlotSwapDragHandler.ShouldHighlightSwapTarget(page, index));

        var showEmptyBorder = Config.ShowEmptySlotBorder ?? true;
        var drawBaseFrame = isEditMode || slot.IsConfigured || showEmptyBorder;
        if (drawBaseFrame)
            SlotChromeDrawer.DrawBaseFrame(drawList, topLeft, topLeft + size, isGrayedOut);

        var drewIcon = PluginShortcuts.TryGetIcon(slot, out var pluginTexture)
            && SafeTextureDraw.TryAddImage(drawList, pluginTexture, topLeft, topLeft + size, iconTint);
        if (!drewIcon)
            drewIcon = TryDrawSlotIcon(drawList, topLeft, size, icon, slotType, iconTint);
        if (!drewIcon)
            drewIcon = DalamudShortcuts.TryDrawIcon(drawList, topLeft, topLeft + size, slot, iconTint);
        if (!drewIcon && slot.Kind == PanelSlotKind.Plugin)
        {
            if (PluginShortcuts.IsIconDownloading(slot.PluginInternalName))
                DrawPluginProcessingOverlay(drawList, topLeft, topLeft + size);
            else
            {
                const string placeholder = "?";
                var textSize = ImGui.CalcTextSize(placeholder);
                var textPos = topLeft + (size - textSize) * 0.5f;
                drawList.AddText(textPos, ImGui.GetColorU32(ImGuiCol.TextDisabled), placeholder);
            }
        }
        if (drewIcon)
            SlotChromeDrawer.DrawIconFrame(drawList, topLeft, topLeft + size, isGrayedOut);

        if (drewIcon && cooldown.IsActive)
            SlotCooldownRenderer.Draw(drawList, topLeft, topLeft + size, cooldown);
        if (pluginVisual == PluginShortcutVisual.Processing)
            DrawPluginProcessingOverlay(drawList, topLeft, topLeft + size);

        if (drewIcon && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            SlotChromeDrawer.DrawHoverFrame(drawList, topLeft, topLeft + size, isGrayedOut: isGrayedOut);

        if (showSelected)
        {
            var selectionColor = ImGui.ColorConvertFloat4ToU32(Config.SelectedSlotBorderColor);
            drawList.AddRect(topLeft, topLeft + size, selectionColor, 4f, ImDrawFlags.None, 2f);
        }

        DrawSlotOverlays(overlay, isGrayedOut, cooldown);

        if (isDropTarget)
            SlotChromeDrawer.DrawDropTargetOverlay(drawList, topLeft, topLeft + size, isGrayedOut);

        ImGui.PopStyleVar();

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            var tooltip = SlotIconResolver.ResolveTooltip(slot);
            if (!string.IsNullOrWhiteSpace(tooltip))
                DrawSlotTooltip(tooltip, topLeft, size);
        }

        if (gameDropClick)
            return;

        HandleSlotClicks(slot, page, index);

        if (isEditMode)
        {
            if (SlotDragDropHandler.TryActivateSlot(page, index))
                onSelect();
            return;
        }

        if (clickWithoutDrag && SlotDragDropHandler.CanActivateSlot(page, index))
            onClick();
    }

    private static void HandleSlotClicks(PanelSlot slot, int page, int index)
    {
        var hoveredForMenu = ImGui.IsItemHovered(
            ImGuiHoveredFlags.AllowWhenDisabled | ImGuiHoveredFlags.AllowWhenBlockedByPopup);
        if (!hoveredForMenu || PanelContextMenu.IsMouseOverMenu)
            return;

        var io = ImGui.GetIO();
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            _rightPressedSlot = slot;
        if (slot.Kind == PanelSlotKind.Plugin && slot.IsConfigured)
        {
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Middle))
                _middlePressedSlot = slot;
            if (!io.KeyShift
                && ImGui.IsMouseReleased(ImGuiMouseButton.Middle)
                && ReferenceEquals(_middlePressedSlot, slot))
                PluginShortcuts.Execute(slot, ImGuiMouseButton.Middle);
        }

        if (io.KeyShift
            || ImGui.IsMouseDown(ImGuiMouseButton.Left)
            || !ImGui.IsMouseReleased(ImGuiMouseButton.Right)
            || !ReferenceEquals(_rightPressedSlot, slot))
            return;

        if (slot.Kind == PanelSlotKind.Plugin
            && slot.IsConfigured
            && !Config.PluginRightClickOpensSlotMenu)
        {
            _pluginRightClickConsumed = true;
            PluginShortcuts.Execute(slot, ImGuiMouseButton.Right);
            return;
        }

        _slotContextClickedThisFrame = true;
        PanelContextMenu.SetSlotContext(page, index);
    }

    private static void DrawSlotTooltip(string tooltip, Vector2 slotTopLeft, Vector2 slotSize)
    {
        const float gapPixels = 12f;
        var anchor = slotTopLeft + new Vector2(slotSize.X * 0.5f, -gapPixels);
        ImGui.SetNextWindowPos(anchor, ImGuiCond.Always, new Vector2(0.5f, 1f));

        using (ImRaii.PushColor(ImGuiCol.PopupBg, Config.TooltipBgColor))
        using (ImRaii.PushColor(ImGuiCol.Text, Config.TooltipTextColor))
        {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8f);
            ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 8f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(Config.TooltipPadding, Config.TooltipPadding));
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

    private static void DrawPluginProcessingOverlay(ImDrawListPtr drawList, Vector2 topLeft, Vector2 bottomRight)
    {
        var iconText = FontAwesomeIcon.Sync.ToIconString();
        var iconFont = UiBuilder.IconFont;
        var fontSize = Math.Max(12f, Config.SlotSize * 0.4f);
        Vector2 textSize;
        using (ImRaii.PushFont(iconFont))
            textSize = ImGui.CalcTextSize(iconText) * (fontSize / ImGui.GetFontSize());
        var pos = topLeft + ((bottomRight - topLeft - textSize) * 0.5f);
        SlotOverlayTextRenderer.Draw(
            drawList,
            iconFont,
            fontSize,
            pos,
            iconText,
            Config.MacroGearLabelStyle,
            OverlayLabelLetterSpacing.Default,
            isGrayedOut: false);
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
        var fontSize = SlotOverlayFontSizeResolver.ResolveFontSize(Config.SlotSize, Config.MacroGearLabelStyle);
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
            Config.MacroGearLabelStyle,
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
            Config.QuantityLabelStyle,
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
            Config.ChargeLabelStyle,
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
        var fontSize = SlotOverlayFontSizeResolver.ResolveFontSize(Config.SlotSize, style);
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
