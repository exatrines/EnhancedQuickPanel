using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using EnhancedQuickPanel.Models;
using EnhancedQuickPanel.Services;
using EnhancedQuickPanel.Services.CustomIcons;
using EnhancedQuickPanel.Services.MacroIcon;
using MirageUI.Theme;

namespace EnhancedQuickPanel.UI;

/// <summary>Popup UI for choosing a slot icon from game icons or user-imported custom images.</summary>
internal static class SlotIconPicker
{
    private const float WindowBorderRounding = 5f;

    private const ImGuiWindowFlags PickerWindowFlags =
        ImGuiWindowFlags.NoCollapse
        | ImGuiWindowFlags.NoResize
        | ImGuiWindowFlags.NoScrollbar
        | ImGuiWindowFlags.NoScrollWithMouse
        | ImGuiWindowFlags.NoSavedSettings;

    /// <summary>Layout constants for the icon picker popup.</summary>
    private static class Layout
    {
        public const float IconCellSize = 40f;
        public const float CategoryComboWidth = 150f;
        public const float GridColumnSpacing = 2f;
        public const int IconsPerRow = 10;
        public const int IconsPerPage = 50;
        public const int GridRows = 5;
        public const float CustomIconNameWidth = 120f;
    }

    private static string _customIconUrl = string.Empty;
    private static string _customIconName = string.Empty;
    private static string _customIconStatus = string.Empty;
    private static bool _customIconDownloadInProgress;
    private static int _customIconPage;
    private static Task<(bool Success, string Message)>? _customIconDownloadTask;

    private static PanelSlot? _targetSlot;
    private static bool _isOpen;
    private static string _search = string.Empty;
    private static MacroIconCategory _category = MacroIconCategory.MacroIcon;
    private static string _manualIconIdText = "0";
    private static uint _manualIconInputKey;
    private static int _resultPage;
    /// <summary>Which icon source tab is currently active in the picker.</summary>
    private enum IconPickerSource
    {
        Custom,
        Ff14,
    }

    private static IconPickerSource _iconSource = IconPickerSource.Ff14;
    private static List<MacroIconEntry> _displayResults = [];

    public static void Open(PanelSlot slot)
    {
        _targetSlot = slot;
        _manualIconIdText = FormatIconIdForInput(slot.IconId);
        _manualIconInputKey = slot.IconId;
        _search = string.Empty;
        _category = MacroIconCategory.MacroIcon;
        _resultPage = 0;
        RefreshResults();
        _iconSource = CustomIconIds.IsCustom(slot.IconId)
            ? IconPickerSource.Custom
            : IconPickerSource.Ff14;
        _isOpen = true;
    }

    public static void Close()
    {
        _isOpen = false;
        _targetSlot = null;
    }

    public static void Draw()
    {
        if (!_isOpen || _targetSlot == null)
            return;

        var slot = _targetSlot;

        using var appearance = new PickerAppearanceScope();
        ImGui.SetNextWindowSize(ComputePickerSize(), ImGuiCond.Always);

        if (!ImGui.Begin(T("iconPicker.title") + "##eqpIconPicker", ref _isOpen, PickerWindowFlags))
        {
            ImGui.End();
            return;
        }

        DrawWindowContent(slot);
        ImGui.End();

        if (!_isOpen)
            _targetSlot = null;
    }

    private static Vector2 ComputePickerSize()
    {
        C.EnsureDefaults();
        var pad = C.WindowPadding;
        var style = ImGui.GetStyle();
        var gridW = ComputeGridWidth();
        var contentH = ComputeContentHeight();
        var titleBarH = ImGui.GetFontSize() + style.FramePadding.Y * 2f;

        return new Vector2(gridW + pad * 2f, contentH + pad * 2f + titleBarH);
    }

    private static float ComputeContentHeight()
    {
        var rowStep = ImGui.GetFrameHeightWithSpacing();
        return rowStep * 4f + ComputeGridHeight() + ComputeBottomSpacing();
    }

    private static float ComputeBottomSpacing()
    {
        C.EnsureDefaults();
        return C.WindowPadding;
    }

    private static float ComputeGridWidth() =>
        Layout.IconsPerRow * Layout.IconCellSize
        + (Layout.IconsPerRow - 1) * Layout.GridColumnSpacing;

    private static float ComputeGridHeight() =>
        Layout.GridRows * Layout.IconCellSize
        + Math.Max(0, Layout.GridRows - 1) * C.SlotPadding;

    private static void DrawWindowContent(PanelSlot slot)
    {
        DrawIconIdRow(slot);
        DrawSourceRadioRow();

        if (_iconSource == IconPickerSource.Ff14)
        {
            DrawCategoryAndSearchBar();
            DrawMainPagingRow();
            DrawResultGrid(slot);
        }
        else
        {
            DrawCustomIconToolbarRow();
            DrawCustomPagingRow();
            DrawCustomIconGrid(slot);
        }

        ImGui.Dummy(new Vector2(0f, ComputeBottomSpacing()));
    }

    private static void DrawSourceRadioRow()
    {
        var source = (int)_iconSource;
        if (ImGui.RadioButton(T("iconPicker.ff14"), ref source, (int)IconPickerSource.Ff14))
            _iconSource = IconPickerSource.Ff14;

        ImGui.SameLine();
        if (ImGui.RadioButton(T("iconPicker.custom"), ref source, (int)IconPickerSource.Custom))
            _iconSource = IconPickerSource.Custom;
    }

    private static void DrawIconIdRow(PanelSlot slot)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var actionSize = IconActionButtonSize;
        var inputWidth = Math.Max(
            120f,
            ImGui.GetContentRegionAvail().X - actionSize.X * 2f - spacing * 2f);

        ImGui.SetNextItemWidth(inputWidth);
        ImGui.PushID(_manualIconInputKey);
        var manualIconIdText = _manualIconIdText;
        if (ImGui.InputText("##eqpIconId", ref manualIconIdText, 64))
            _manualIconIdText = manualIconIdText;

        if (ImGui.IsKeyPressed(ImGuiKey.Enter)
            && TryParseIconIdInput(_manualIconIdText, out var enterIconId))
            ApplyIcon(slot, enterIconId);

        ImGui.PopID();

        ImGui.SameLine(0f, spacing);
        if (DrawPickerIconButton(FontAwesomeIcon.Check, "##eqpIconApply", T("common.apply"))
            && TryParseIconIdInput(_manualIconIdText, out var applyIconId))
            ApplyIcon(slot, applyIconId);

        ImGui.SameLine(0f, spacing);
        if (DrawPickerIconButton(FontAwesomeIcon.Ban, "##eqpIconClear", T("iconPicker.clear")))
            ApplyIcon(slot, 0);
    }

    private static void DrawCategoryAndSearchBar()
    {
        ImGui.SetNextItemWidth(Layout.CategoryComboWidth);
        if (ImGui.BeginCombo("##eqpIconCategory", _category.DisplayName()))
        {
            foreach (var category in MacroIconCategoryExtensions.PickerCategories)
            {
                if (!ImGui.Selectable(category.DisplayName(), _category == category))
                    continue;

                _category = category;
                _resultPage = 0;
                RefreshResults();
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1f);
        var search = _search;
        if (ImGui.InputTextWithHint("##eqpIconSearch", T("iconPicker.search"), ref search, 128))
        {
            _search = search;
            _resultPage = 0;
            RefreshResults();
        }
    }

    private static void DrawMainPagingRow() =>
        DrawPagingRow(
            ref _resultPage,
            GetPageCount(),
            "##eqpIconPrev",
            "##eqpIconNext",
            "##eqpIconPageWheel",
            ApplyPageWheel);

    private static void DrawCustomPagingRow() =>
        DrawPagingRow(
            ref _customIconPage,
            GetCustomPageCount(),
            "##eqpCustomIconPrev",
            "##eqpCustomIconNext",
            "##eqpCustomIconPageWheel",
            ApplyCustomPageWheel);

    private static void DrawPagingRow(
        ref int page,
        int pageCount,
        string prevId,
        string nextId,
        string wheelId,
        Action<int> onWheel)
    {
        page = Math.Clamp(page, 0, Math.Max(0, pageCount - 1));

        var canGoPrev = page > 0;
        using (ImRaii.Disabled(!canGoPrev))
        {
            if (ImGui.ArrowButton(prevId, ImGuiDir.Left) && canGoPrev)
                page--;
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            onWheel(pageCount);

        ImGui.SameLine();
        DrawPageIndicator(wheelId, page, pageCount, onWheel);

        var canGoNext = page < pageCount - 1;
        ImGui.SameLine();
        using (ImRaii.Disabled(!canGoNext))
        {
            if (ImGui.ArrowButton(nextId, ImGuiDir.Right) && canGoNext)
                page++;
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            onWheel(pageCount);
    }

    private static void DrawPageIndicator(string wheelId, int page, int pageCount, Action<int> onWheel)
    {
        var label = $"{page + 1} / {pageCount}";
        var labelSize = ImGui.CalcTextSize(label);
        var hitSize = new Vector2(labelSize.X, ImGui.GetFrameHeight());

        ImGui.InvisibleButton(wheelId, hitSize);
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            onWheel(pageCount);

        var textPos = ImGui.GetItemRectMin();
        textPos.Y += (hitSize.Y - labelSize.Y) * 0.5f;
        ImGui.GetWindowDrawList().AddText(textPos, ImGui.GetColorU32(ImGuiCol.Text), label);
    }

    private static void ApplyPageWheel(int pageCount)
    {
        var wheel = ImGui.GetIO().MouseWheel;
        if (wheel > 0f)
            _resultPage = Math.Max(0, _resultPage - 1);
        else if (wheel < 0f)
            _resultPage = Math.Min(pageCount - 1, _resultPage + 1);
    }

    private static int GetPageCount()
    {
        if (_displayResults.Count == 0)
            return 1;

        return Math.Max(1, (_displayResults.Count + Layout.IconsPerPage - 1) / Layout.IconsPerPage);
    }

    private static void DrawResultGrid(PanelSlot slot)
    {
        var pageCount = GetPageCount();
        _resultPage = Math.Clamp(_resultPage, 0, Math.Max(0, pageCount - 1));
        var start = _resultPage * Layout.IconsPerPage;

        DrawFixedIconGrid(
            "##eqpIconGrid",
            cellIndex =>
            {
                var itemIndex = start + cellIndex;
                if (itemIndex >= _displayResults.Count)
                {
                    DrawEmptyIconCell();
                    return;
                }

                DrawSelectableIcon(slot, _displayResults[itemIndex]);
            });
    }

    private static void DrawFixedIconGrid(string id, Action<int> drawCell)
    {
        using (ImRaii.PushId(id))
        {
            ImGui.BeginGroup();
            for (var row = 0; row < Layout.GridRows; row++)
            {
                if (row > 0)
                    ImGui.Spacing();

                for (var col = 0; col < Layout.IconsPerRow; col++)
                {
                    if (col > 0)
                        ImGui.SameLine(0f, Layout.GridColumnSpacing);

                    var cellIndex = row * Layout.IconsPerRow + col;
                    using (ImRaii.PushId(cellIndex))
                        drawCell(cellIndex);
                }
            }

            ImGui.EndGroup();
        }
    }

    private static void DrawEmptyIconCell() =>
        ImGui.Dummy(new Vector2(Layout.IconCellSize, Layout.IconCellSize));

    private static void DrawCustomIconToolbarRow()
    {
        ProcessCustomIconDownloadTask();

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var actionSize = IconActionButtonSize.X;
        var nameWidth = Layout.CustomIconNameWidth;
        const float separatorWidth = 1f;
        var urlWidth = Math.Max(
            80f,
            ImGui.GetContentRegionAvail().X
            - actionSize * 3f
            - nameWidth
            - separatorWidth
            - spacing * 5f);

        if (DrawPickerIconButton(FontAwesomeIcon.FolderOpen, "##eqpCustomIconFolderOpen", T("iconPicker.openFolder")))
            CustomIconRegistry.OpenIconFolder();

        ImGui.SameLine(0f, spacing);
        if (DrawPickerIconButton(FontAwesomeIcon.Sync, "##eqpCustomIconFolderRefresh", T("iconPicker.refreshFolder")))
        {
            CustomIconRegistry.RefreshFromDisk();
            _customIconPage = Math.Clamp(_customIconPage, 0, Math.Max(0, GetCustomPageCount() - 1));
        }

        DrawInlineVerticalSeparator(ImGui.GetFrameHeight());

        ImGui.SameLine(0f, spacing);
        ImGui.SetNextItemWidth(urlWidth);
        var url = _customIconUrl;
        if (ImGui.InputTextWithHint("##eqpCustomIconUrl", T("iconPicker.imageUrl"), ref url, 512))
            _customIconUrl = url;

        ImGui.SameLine(0f, spacing);
        ImGui.SetNextItemWidth(nameWidth);
        var name = _customIconName;
        if (ImGui.InputTextWithHint("##eqpCustomIconName", T("common.name"), ref name, 64))
            _customIconName = name;

        ImGui.SameLine(0f, spacing);
        using (ImRaii.Disabled(_customIconDownloadInProgress))
        {
            var saveTooltip = string.IsNullOrWhiteSpace(_customIconStatus) ? T("common.save") : _customIconStatus;
            if (DrawPickerIconButton(
                    FontAwesomeIcon.Download,
                    "##eqpCustomIconSave",
                    saveTooltip,
                    enabled: !_customIconDownloadInProgress))
                StartCustomIconDownload();
        }
    }

    private static void DrawInlineVerticalSeparator(float height)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        ImGui.SameLine(0f, spacing);

        var pos = ImGui.GetCursorScreenPos();
        var x = pos.X + spacing * 0.5f;
        ImGui.GetWindowDrawList().AddLine(
            new Vector2(x, pos.Y),
            new Vector2(x, pos.Y + height),
            ImGui.GetColorU32(ImGuiCol.Separator));
        ImGui.Dummy(new Vector2(spacing, height));
    }

    private static Vector2 IconActionButtonSize =>
        new(ImGui.GetFrameHeight(), ImGui.GetFrameHeight());

    private static bool DrawPickerIconButton(
        FontAwesomeIcon icon,
        string id,
        string tooltip,
        bool enabled = true)
    {
        var textColor = MirageUi.GetColor(MirageUi.Color.Default);
        var hoveredTextColor = MirageUi.GetColor(MirageUi.Color.Accent);
        var clicked = CenteredIconButton.Draw(
            icon,
            id,
            IconActionButtonSize,
            textColor,
            hoveredTextColor,
            enabled);

        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            return clicked;

        SetMirageTooltip(tooltip);
        return clicked;
    }

    private static void SetMirageTooltip(string tooltip)
    {
        var settings = MirageTheme.Active ?? MirageTheme.ResolveAppliedColors();
        using (ImRaii.PushColor(ImGuiCol.PopupBg, settings.WindowBg))
        using (ImRaii.PushColor(ImGuiCol.Text, MirageUi.GetColor(MirageUi.Color.Default)))
            ImGui.SetTooltip(tooltip);
    }

    private static void StartCustomIconDownload()
    {
        if (_customIconDownloadInProgress)
            return;

        _customIconDownloadInProgress = true;
        _customIconStatus = T("iconPicker.downloading");
        var url = _customIconUrl;
        var name = _customIconName;
        _customIconDownloadTask = CustomIconRegistry.DownloadAndSaveAsync(url, name);
    }

    private static void ProcessCustomIconDownloadTask()
    {
        if (_customIconDownloadTask is not { IsCompleted: true } task)
            return;

        var (success, message) = task.Result;
        _customIconDownloadTask = null;
        _customIconDownloadInProgress = false;
        _customIconStatus = success ? string.Empty : message;

        if (!success)
            return;

        _customIconUrl = string.Empty;
        _customIconName = string.Empty;
        _customIconPage = Math.Max(0, GetCustomPageCount() - 1);
    }

    private static void DrawCustomIconGrid(PanelSlot slot)
    {
        var icons = CustomIconRegistry.SortedIcons;
        var pageCount = GetCustomPageCount();
        _customIconPage = Math.Clamp(_customIconPage, 0, Math.Max(0, pageCount - 1));
        var start = _customIconPage * Layout.IconsPerPage;

        DrawFixedIconGrid(
            "##eqpCustomIconGrid",
            cellIndex =>
            {
                var itemIndex = start + cellIndex;
                if (itemIndex >= icons.Count)
                {
                    DrawEmptyIconCell();
                    return;
                }

                DrawSelectableCustomIcon(slot, icons[itemIndex]);
            });
    }

    private static void DrawSelectableCustomIcon(PanelSlot slot, CustomIconEntry entry)
    {
        var iconId = CustomIconRegistry.ToIconId(entry);
        var selected = slot.IconId == iconId;

        ImGui.PushID(entry.FileName);
        ImGui.InvisibleButton("##eqpCustomIconCell", new Vector2(Layout.IconCellSize, Layout.IconCellSize));
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            ApplyIcon(slot, iconId);
        ImGui.PopID();

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            var tooltip = $"{entry.Name}\nID: {CustomIconRegistry.FormatDisplayId(entry)}";
            SetMirageTooltip(tooltip);
        }

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        if (selected)
        {
            var selectionColor = ImGui.ColorConvertFloat4ToU32(MirageUi.GetColor(MirageUi.Color.Accent));
            ImGui.GetWindowDrawList().AddRect(min, max, selectionColor, 3f, ImDrawFlags.None, 2f);
        }

        DrawIconInRect(iconId, min, max);
    }

    private static void ApplyCustomPageWheel(int pageCount)
    {
        var wheel = ImGui.GetIO().MouseWheel;
        if (wheel > 0f)
            _customIconPage = Math.Max(0, _customIconPage - 1);
        else if (wheel < 0f)
            _customIconPage = Math.Min(pageCount - 1, _customIconPage + 1);
    }

    private static int GetCustomPageCount()
    {
        var count = CustomIconRegistry.SortedIcons.Count;
        if (count == 0)
            return 1;

        return Math.Max(1, (count + Layout.IconsPerPage - 1) / Layout.IconsPerPage);
    }

    private static string FormatIconIdForInput(uint iconId) =>
        CustomIconIds.IsCustom(iconId)
            ? CustomIconRegistry.FormatDisplayId(iconId)
            : iconId.ToString();

    private static bool TryParseIconIdInput(string text, out uint iconId)
    {
        if (CustomIconRegistry.TryParseIconId(text, out iconId))
            return true;

        return uint.TryParse(text.Trim(), out iconId);
    }

    private static void DrawSelectableIcon(PanelSlot slot, MacroIconEntry entry)
    {
        if (slot == null)
            return;

        var iconId = entry.IconId;
        var selected = slot.IconId == iconId;

        ImGui.PushID((int)iconId ^ (int)entry.RowId ^ (int)entry.Category);
        ImGui.InvisibleButton("##eqpIconCell", new Vector2(Layout.IconCellSize, Layout.IconCellSize));
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            ApplyIcon(slot, iconId);
        ImGui.PopID();

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            var tooltip = $"{entry.Name}\n{entry.Category.DisplayName()}\nID: {iconId}";
            if (!string.IsNullOrEmpty(entry.Detail))
                tooltip += $"\n{entry.Detail}";

            SetMirageTooltip(tooltip);
        }

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        if (selected)
        {
            var selectionColor = ImGui.ColorConvertFloat4ToU32(MirageUi.GetColor(MirageUi.Color.Accent));
            ImGui.GetWindowDrawList().AddRect(min, max, selectionColor, 3f, ImDrawFlags.None, 2f);
        }
        DrawIconInRect(iconId, min, max);
    }

    private static void SelectIcon(uint iconId)
    {
        _manualIconIdText = FormatIconIdForInput(iconId);
        _manualIconInputKey = iconId;
    }

    public static void DrawIconButton(PanelSlot slot, Vector2 size, bool interactive = true)
    {
        var previewIcon = slot.IconId != 0
            ? new ResolvedSlotIcon(slot.IconId, false)
            : SlotIconResolver.ResolveIcon(slot);

        if (interactive)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.FrameBg));
            if (ImGui.Button("##eqpOverlayIconPick", size))
                Open(slot);
            ImGui.PopStyleColor();
        }
        else
        {
            using (ImRaii.Disabled())
                ImGui.Button("##eqpOverlayIconPick", size);
        }

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRect(min, max, ImGui.GetColorU32(ImGuiCol.Border), 4f);

        if (previewIcon.IsValid)
        {
            DrawIconInRect(previewIcon.IconId, min, max);
            return;
        }

        var text = "?";
        var textSize = ImGui.CalcTextSize(text);
        var pos = min + (max - min - textSize) * 0.5f;
        drawList.AddText(pos, ImGui.GetColorU32(ImGuiCol.TextDisabled), text);
    }

    public static void DrawIconDisplay(PanelSlot slot, Vector2 size)
    {
        var previewIcon = slot.IconId != 0
            ? new ResolvedSlotIcon(slot.IconId, false)
            : SlotIconResolver.ResolveIcon(slot);

        ImGui.Dummy(size);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRect(min, max, ImGui.GetColorU32(ImGuiCol.Border), 4f);

        if (previewIcon.IsValid)
        {
            DrawIconInRect(previewIcon.IconId, min, max);
            return;
        }

        var text = "?";
        var textSize = ImGui.CalcTextSize(text);
        var pos = min + (max - min - textSize) * 0.5f;
        drawList.AddText(pos, ImGui.GetColorU32(ImGuiCol.TextDisabled), text);
    }

    private static void DrawIconInRect(uint iconId, Vector2 min, Vector2 max)
    {
        if (iconId == 0)
            return;

        var icon = new ResolvedSlotIcon(iconId, false);
        if (!SlotTextureResolver.TryGetSlotTexture(icon, out var texture))
            return;

        var drawList = ImGui.GetWindowDrawList();
        var pad = 3f;
        SafeTextureDraw.TryAddImage(drawList, texture, min + new Vector2(pad, pad), max - new Vector2(pad, pad), uint.MaxValue);
    }

    private static void ApplyIcon(PanelSlot slot, uint iconId)
    {
        slot.IconId = iconId;
        SelectIcon(iconId);
        EzConfig.Save();
        _isOpen = false;
    }

    public static string ResolveIconDisplayName(uint iconId)
    {
        if (iconId == 0)
            return T("common.auto");

        if (CustomIconIds.IsCustom(iconId))
        {
            var customName = CustomIconRegistry.GetName(iconId);
            return string.IsNullOrWhiteSpace(customName)
                ? CustomIconRegistry.FormatDisplayId(iconId)
                : customName;
        }

        var names = LookupIconNames(iconId);
        return names.Count == 0
            ? T("common.unknown")
            : string.Join(" / ", names);
    }

    private static void RefreshResults()
    {
        _displayResults = string.IsNullOrWhiteSpace(_search)
            ? MacroIconLookup.GetCategoryEntries(_category).ToList()
            : MacroIconLookup.SearchAll(_search).ToList();
    }

    private static List<string> LookupIconNames(uint iconId) =>
        MacroIconLookup.GetNamesForIconId(iconId).ToList();

    /// <summary>
    /// Applies MirageUI theme colors to the picker without leaking styles to other windows.
    /// </summary>
    /// <summary>Pushes icon-picker window styling and restores it on dispose.</summary>
    private readonly struct PickerAppearanceScope : IDisposable
    {
        private readonly ImRaii.ColorDisposable? _theme;
        private readonly ImRaii.ColorDisposable? _popupBg;
        private readonly ImRaii.ColorDisposable? _border;
        private readonly ImRaii.ColorDisposable? _button;
        private readonly ImRaii.ColorDisposable? _buttonHovered;
        private readonly ImRaii.ColorDisposable? _buttonActive;
        private readonly ImRaii.ColorDisposable? _frameBgActive;
        private readonly ImRaii.StyleDisposable _windowBorderSize;
        private readonly ImRaii.StyleDisposable _windowRounding;
        private readonly ImRaii.StyleDisposable _windowPadding;
        private readonly ImRaii.StyleDisposable _itemSpacing;

        public PickerAppearanceScope()
        {
            MirageTheme.EnsureDefaultsCaptured();
            var settings = MirageTheme.ResolveAppliedColors();
            _theme = MirageTheme.PushCustom(settings);
            _popupBg = ImRaii.PushColor(ImGuiCol.PopupBg, settings.WindowBg);
            _border = ImRaii.PushColor(ImGuiCol.Border, settings.GetColor(MirageUi.Color.Accent));
            _button = ImRaii.PushColor(ImGuiCol.Button, settings.Header);
            _buttonHovered = ImRaii.PushColor(ImGuiCol.ButtonHovered, settings.HeaderHovered);
            _buttonActive = ImRaii.PushColor(ImGuiCol.ButtonActive, settings.HeaderActive);
            _frameBgActive = ImRaii.PushColor(ImGuiCol.FrameBgActive, settings.FrameBgHovered);

            C.EnsureDefaults();
            _windowBorderSize = ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 0f);
            _windowRounding = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, WindowBorderRounding);
            _windowPadding = ImRaii.PushStyle(
                ImGuiStyleVar.WindowPadding,
                new Vector2(C.WindowPadding, C.WindowPadding));
            _itemSpacing = ImRaii.PushStyle(
                ImGuiStyleVar.ItemSpacing,
                new Vector2(C.SlotPadding, C.SlotPadding));
        }

        public void Dispose()
        {
            _itemSpacing.Dispose();
            _windowPadding.Dispose();
            _windowRounding.Dispose();
            _windowBorderSize.Dispose();
            _frameBgActive?.Dispose();
            _buttonActive?.Dispose();
            _buttonHovered?.Dispose();
            _button?.Dispose();
            _border?.Dispose();
            _popupBg?.Dispose();
            MirageTheme.Pop(_theme);
        }
    }
}
