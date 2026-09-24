using System.Runtime.InteropServices;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using EnhancedQuickPanel.Models;
using EnhancedQuickPanel.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace EnhancedQuickPanel.UI;

/// <summary>Overlay right-click menu: panel actions, then slot actions when a slot was clicked.</summary>
internal static class PanelContextMenu
{
    private const string PopupId = "##eqpPanelContext";
    private const int SubmenuScrollAfterRows = 5;
    private const ImGuiWindowFlags WindowFlags =
        ImGuiWindowFlags.NoResize
        | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoTitleBar
        | ImGuiWindowFlags.NoSavedSettings;

    private static int _slotPage = -1;
    private static int _slotIndex = -1;
    private static Vector2? _anchor;
    private static Vector2 _menuMin;
    private static Vector2 _menuMax;
    private static bool _hasMenuRect;
    private static Vector2 _submenuMin;
    private static Vector2 _submenuMax;
    private static bool _hasSubmenuRect;

    public static void SetSlotContext(int page, int index)
    {
        _slotPage = page;
        _slotIndex = index;
    }

    public static bool IsOpen => ImGui.IsPopupOpen(PopupId);

    public static bool IsMouseOverMenu
    {
        get
        {
            if (!_hasMenuRect && !_hasSubmenuRect)
                return false;
            var mouse = ImGui.GetIO().MousePos;
            return Contains(mouse, _menuMin, _menuMax, _hasMenuRect)
                || Contains(mouse, _submenuMin, _submenuMax, _hasSubmenuRect);
        }
    }

    public static void Draw(
        bool pluginRightClickConsumed,
        bool slotClickedThisFrame,
        bool isEditing,
        Action importPage,
        Action exportPage,
        Action<int> importNative,
        Action toggleEdit,
        Action toggleCollapse,
        Action closeOverlay,
        Action<int, int> editSlot,
        int selectedPage,
        Action<int> switchPage)
    {
        Config.EnsureDefaults();
        if (!ImGui.IsPopupOpen(PopupId) && !slotClickedThisFrame)
            ClearSlotContext();

        var hasPanel = Config.ContextMenuItems.HasVisibleItems;
        var hasSlot = TryGetSlotModel(out var slot);
        if (!hasPanel && !hasSlot)
            return;

        TryOpen(pluginRightClickConsumed, slotClickedThisFrame);

        var panelRows = hasPanel
            ? BuildPanelRows(
                isEditing,
                importPage,
                exportPage,
                importNative,
                toggleEdit,
                toggleCollapse,
                closeOverlay,
                selectedPage,
                switchPage)
            : new List<MenuActionRow>();
        var slotRows = hasSlot ? BuildSlotRows(slot, editSlot) : new List<MenuActionRow>();

        var labels = new List<string>(16);
        if (hasPanel)
        {
            labels.Add(PluginServices.PluginInterface.Manifest.Name);
            AddRowLabels(labels, panelRows);
        }

        if (hasSlot)
        {
            labels.Add(slot.Name);
            AddRowLabels(labels, slotRows);
        }

        var style = Config.ContextMenu;
        style.EnsureDefaults();
        var labelSpan = CollectionsMarshal.AsSpan(labels);
        var rowHeight = ContextMenuItem.ComputeRowHeight(style, labelSpan);
        var menuWidth = ContextMenuItem.ComputeRequiredWidth(style, labelSpan);
        var separatorCount = hasPanel && hasSlot ? 1 : 0;
        var windowHeight = rowHeight * labels.Count
            + ContextMenuItem.SeparatorHeight * separatorCount
            + style.Padding * 2f;

        if (_anchor is { } anchor)
            ImGui.SetNextWindowPos(anchor, ImGuiCond.Always, new Vector2(0f, 1f));
        ImGui.SetNextWindowSize(new Vector2(menuWidth, windowHeight), ImGuiCond.Always);

        using (new ContextMenuStyleScope(style))
        {
            if (ImGui.BeginPopup(PopupId, WindowFlags))
            {
                var pos = ImGui.GetWindowPos();
                _menuMin = pos;
                _menuMax = pos + ImGui.GetWindowSize();
                _hasMenuRect = true;
                _hasSubmenuRect = false;

                if (hasPanel)
                {
                    ContextMenuItem.DrawHeader(PluginServices.PluginInterface.Manifest.Name, style, rowHeight);
                    if (DrawRows(panelRows, style, rowHeight))
                    {
                        ImGui.CloseCurrentPopup();
                        _anchor = null;
                    }
                }

                if (hasSlot)
                {
                    if (hasPanel)
                        ContextMenuItem.DrawSeparator(style);
                    ContextMenuItem.DrawHeader(slot.Name, style, rowHeight);
                    DrawRows(slotRows, style, rowHeight);
                }

                ImGui.EndPopup();
            }
            else
                ClearHoverRects();
        }

        if (!ImGui.IsPopupOpen(PopupId))
        {
            ClearHoverRects();
            if (!slotClickedThisFrame)
            {
                _anchor = null;
                ClearSlotContext();
            }
            else
                ImGui.OpenPopup(PopupId);
        }
    }

    private static void ClearSlotContext()
    {
        _slotPage = -1;
        _slotIndex = -1;
    }

    private static void ClearHoverRects()
    {
        _hasMenuRect = false;
        _hasSubmenuRect = false;
    }

    private static void TryOpen(bool pluginRightClickConsumed, bool slotClickedThisFrame)
    {
        if (pluginRightClickConsumed)
            return;
        if (!ImGui.IsMouseReleased(ImGuiMouseButton.Right))
            return;

        if (!slotClickedThisFrame)
        {
            if (!ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows))
                return;
            if (ImGui.IsPopupOpen(PopupId))
                return;
        }

        _anchor = ImGui.GetIO().MousePos;
        ImGui.OpenPopup(PopupId);
    }

    private static void AddRowLabels(List<string> labels, List<MenuActionRow> rows)
    {
        foreach (var row in rows)
            labels.Add(row.Label);
    }

    private static bool DrawRows(
        List<MenuActionRow> rows,
        ContextMenuStyleConfig style,
        float rowHeight)
    {
        var closeParent = false;
        foreach (var row in rows)
        {
            var hasSubmenu = row.HasSubmenu;
            var flags = hasSubmenu
                ? ImGuiSelectableFlags.DontClosePopups
                : ImGuiSelectableFlags.None;
            var clicked = ContextMenuItem.Draw(
                row.Label,
                row.Icon,
                row.Id,
                style,
                row.TrailingIcon ?? (hasSubmenu ? FontAwesomeIcon.ChevronRight : null),
                row.Enabled,
                row.DisabledTooltip,
                flags);
            if (clicked && hasSubmenu)
                ImGui.OpenPopup(SubmenuPopupId(row.Id));
            else if (clicked)
                row.OnClick?.Invoke();

            if (hasSubmenu && DrawSubmenu(row, style, rowHeight))
                closeParent = true;
        }

        return closeParent;
    }

    private static bool DrawSubmenu(
        in MenuActionRow row,
        ContextMenuStyleConfig style,
        float rowHeight)
    {
        var items = row.SubmenuItems;
        if (items is not { Count: > 0 })
            return false;

        var itemRectMax = ImGui.GetItemRectMax();
        var itemRectMin = ImGui.GetItemRectMin();
        var labels = new string[items.Count];
        for (var i = 0; i < items.Count; i++)
            labels[i] = items[i].Label;

        var width = ContextMenuItem.ComputeRequiredWidth(style, labels);
        var visibleRows = Math.Min(items.Count, SubmenuScrollAfterRows);
        var height = rowHeight * visibleRows + style.Padding * 2f;
        ImGui.SetNextWindowPos(new Vector2(itemRectMax.X, itemRectMin.Y), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(width, height), ImGuiCond.Always);

        var popupId = SubmenuPopupId(row.Id);
        var submenuFlags = WindowFlags | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        if (!ImGui.BeginPopup(popupId, submenuFlags))
            return false;

        var pos = ImGui.GetWindowPos();
        _submenuMin = pos;
        _submenuMax = pos + ImGui.GetWindowSize();
        _hasSubmenuRect = true;

        var chosen = false;
        var useScroll = items.Count > SubmenuScrollAfterRows;
        if (useScroll)
        {
            using var scroll = ImRaii.Child(
                $"{popupId}Scroll",
                new Vector2(ImGui.GetContentRegionAvail().X, rowHeight * SubmenuScrollAfterRows),
                false);
            if (scroll)
                chosen = DrawSubmenuItems(items, style);
        }
        else
        {
            chosen = DrawSubmenuItems(items, style);
        }

        ImGui.EndPopup();
        return chosen;
    }

    private static bool DrawSubmenuItems(IReadOnlyList<MenuActionRow> items, ContextMenuStyleConfig style)
    {
        var chosen = false;
        foreach (var item in items)
        {
            if (!ContextMenuItem.Draw(
                    item.Label,
                    item.Icon,
                    item.Id,
                    style,
                    item.TrailingIcon,
                    item.Enabled,
                    item.DisabledTooltip))
                continue;

            item.OnClick?.Invoke();
            chosen = true;
        }

        return chosen;
    }

    private static string SubmenuPopupId(string rowId) => $"{rowId}Sub";

    private static bool Contains(Vector2 mouse, Vector2 min, Vector2 max, bool enabled) =>
        enabled
        && mouse.X >= min.X && mouse.X < max.X
        && mouse.Y >= min.Y && mouse.Y < max.Y;

    private static List<MenuActionRow> BuildPanelRows(
        bool isEditing,
        Action importPage,
        Action exportPage,
        Action<int> importNative,
        Action toggleEdit,
        Action toggleCollapse,
        Action closeOverlay,
        int selectedPage,
        Action<int> switchPage)
    {
        var items = Config.ContextMenuItems;
        var rows = new List<MenuActionRow>(8);
        if (items.IsSettingsVisible)
            rows.Add(new(T("contextMenu.settings"), FontAwesomeIcon.Cog, "##eqpContextSettings", ToggleConfig));
        if (items.IsSwitchPageVisible)
        {
            var pageItems = BuildSwitchPageItems(selectedPage, switchPage);
            if (pageItems.Count > 0)
            {
                rows.Add(new(
                    T("contextMenu.switchPage"),
                    FontAwesomeIcon.LayerGroup,
                    "##eqpContextSwitchPageItem",
                    null,
                    SubmenuItems: pageItems));
            }
        }
        if (items.IsImportPageVisible)
            rows.Add(new(T("contextMenu.importPage"), FontAwesomeIcon.Download, "##eqpContextImportPage", importPage));
        if (items.IsExportPageVisible)
            rows.Add(new(T("contextMenu.exportPage"), FontAwesomeIcon.Upload, "##eqpContextExportPage", exportPage));
        if (items.IsImportNativeVisible)
        {
            rows.Add(new(T("contextMenu.importNative"), FontAwesomeIcon.FileImport, "##eqpContextImportNative", () =>
            {
                NativeQuickPanelImportPopup.Open(onImported: importNative);
                ImGui.CloseCurrentPopup();
                _anchor = null;
            }));
        }

        if (items.IsEditVisible)
            rows.Add(new(T("contextMenu.edit"), FontAwesomeIcon.Pen, "##eqpContextEdit", toggleEdit, TrailingIcon: isEditing ? FontAwesomeIcon.Check : null));
        if (items.IsCollapseVisible)
            rows.Add(new(PanelCollapse.Label, PanelCollapse.Icon, "##eqpContextCollapse", toggleCollapse));
        if (items.IsCloseVisible)
            rows.Add(new(T("contextMenu.close"), FontAwesomeIcon.Times, "##eqpContextClose", closeOverlay));
        return rows;
    }

    private static List<MenuActionRow> BuildSwitchPageItems(int selectedPage, Action<int> switchPage)
    {
        var pages = Config.Pages;
        var items = new List<MenuActionRow>(pages.Count);
        for (var page = 0; page < pages.Count; page++)
        {
            var pageIndex = page;
            items.Add(new(
                pages[page].DisplayName,
                FontAwesomeIcon.FileAlt,
                $"##eqpContextSwitchPage{pageIndex}",
                () => switchPage(pageIndex),
                TrailingIcon: pageIndex == selectedPage ? FontAwesomeIcon.Check : null));
        }

        return items;
    }

    private static List<MenuActionRow> BuildSlotRows(SlotMenuModel model, Action<int, int> editSlot)
    {
        var slot = model.Slot;
        var shiftHeld = ImGui.GetIO().KeyShift;
        var rows = new List<MenuActionRow>(8);
        if (model.IsPlugin)
        {
            var busy = PluginLifecycleToggle.IsBusy(slot.PluginInternalName, slot.PluginWorkingPluginId);
            var busyHint = T("shortcut.toggleBusy");
            rows.Add(new(
                T("slotMenu.openPluginUi"),
                FontAwesomeIcon.Plug,
                "##eqpSlotOpenUi",
                () => PluginShortcuts.OpenUi(slot, preferMain: true),
                Enabled: !busy,
                DisabledTooltip: busyHint));
            rows.Add(new(
                T("slotMenu.openPluginConfig"),
                FontAwesomeIcon.Cog,
                "##eqpSlotOpenConfig",
                () => PluginShortcuts.OpenUi(slot, preferMain: false),
                Enabled: !busy,
                DisabledTooltip: busyHint));
            if (model.ShowToggle)
            {
                var canToggle = PluginLifecycleToggle.CanToggle(slot.PluginInternalName, slot.PluginWorkingPluginId, out var toggleHint);
                rows.Add(new(
                    T("slotMenu.togglePlugin"),
                    FontAwesomeIcon.PowerOff,
                    "##eqpSlotToggle",
                    () => PluginShortcuts.ToggleEnabled(slot),
                    Enabled: canToggle,
                    DisabledTooltip: toggleHint));
            }
        }
        else if (model.CanExecute)
            rows.Add(new(T("slotMenu.execute"), FontAwesomeIcon.Play, "##eqpSlotExecute", () => SlotExecutor.Execute(slot)));

        if (model.ShowMacros)
            rows.Add(new(T("slotMenu.showMacros"), FontAwesomeIcon.FileAlt, "##eqpSlotMacros", () => TextCommandExecutor.Execute("/macro")));
        if (model.ShowActions)
            rows.Add(new(T("slotMenu.showActions"), FontAwesomeIcon.Book, "##eqpSlotActions", () => TextCommandExecutor.Execute("/action")));
        if (model.ShowInventory)
            rows.Add(new(T("slotMenu.showInventory"), FontAwesomeIcon.BoxOpen, "##eqpSlotInventory", () => TextCommandExecutor.Execute("/inventory")));

        rows.Add(new(T("slotMenu.edit"), FontAwesomeIcon.Pen, "##eqpSlotEdit", () => editSlot(model.Page, model.Index)));
        rows.Add(new(
            T("slotMenu.delete"),
            FontAwesomeIcon.Trash,
            "##eqpSlotDelete",
            () => SlotEditor.ClearSlotContents(slot),
            Enabled: shiftHeld,
            DisabledTooltip: T("slotMenu.deleteHint")));
        return rows;
    }

    private static bool TryGetSlotModel(out SlotMenuModel model)
    {
        model = default;
        Config.EnsureDefaults();
        if (_slotPage < 0 || _slotIndex < 0 || _slotPage >= Config.Pages.Count)
            return false;

        var page = Config.Pages[_slotPage];
        if (_slotIndex >= page.Slots.Count)
            return false;

        var slot = page.Slots[_slotIndex];
        var isPlugin = slot.Kind == PanelSlotKind.Plugin && slot.IsConfigured;
        var isItem = slot.Kind == PanelSlotKind.Action
            && slot.IsConfigured
            && InventorySlotHelper.IsItemSlotType((RaptureHotbarModule.HotbarSlotType)slot.CommandType);
        var name = SlotIconResolver.ResolveTooltip(slot);
        if (string.IsNullOrWhiteSpace(name))
            name = T("common.noName");
        model = new SlotMenuModel(
            slot,
            _slotPage,
            _slotIndex,
            name,
            isPlugin,
            slot.IsConfigured && !isPlugin,
            slot.Kind == PanelSlotKind.Macro && slot.IsConfigured,
            slot.Kind == PanelSlotKind.Action && slot.IsConfigured && !isItem,
            isItem,
            isPlugin && !PluginShortcuts.IsSelf(slot.PluginInternalName));
        return true;
    }

    private readonly record struct MenuActionRow(
        string Label,
        FontAwesomeIcon Icon,
        string Id,
        Action? OnClick,
        FontAwesomeIcon? TrailingIcon = null,
        bool Enabled = true,
        string? DisabledTooltip = null,
        IReadOnlyList<MenuActionRow>? SubmenuItems = null)
    {
        public bool HasSubmenu => SubmenuItems is { Count: > 0 };
    }

    private readonly record struct SlotMenuModel(
        PanelSlot Slot,
        int Page,
        int Index,
        string Name,
        bool IsPlugin,
        bool CanExecute,
        bool ShowMacros,
        bool ShowActions,
        bool ShowInventory,
        bool ShowToggle);
}
