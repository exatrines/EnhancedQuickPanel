using System.Runtime.InteropServices;
using Dalamud.Interface;
using EnhancedQuickPanel.Models;
using EnhancedQuickPanel.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace EnhancedQuickPanel.UI;

/// <summary>Overlay right-click menu: panel actions, then slot actions when a slot was clicked.</summary>
internal static class PanelContextMenu
{
    private const string PopupId = "##eqpPanelContext";
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

    public static void SetSlotContext(int page, int index)
    {
        _slotPage = page;
        _slotIndex = index;
    }

    public static bool IsMouseOverMenu
    {
        get
        {
            if (!_hasMenuRect)
                return false;
            var mouse = ImGui.GetIO().MousePos;
            return mouse.X >= _menuMin.X && mouse.X < _menuMax.X
                && mouse.Y >= _menuMin.Y && mouse.Y < _menuMax.Y;
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
        Action<int, int> editSlot)
    {
        Config.EnsureDefaults();
        if (!ImGui.IsPopupOpen(PopupId) && !slotClickedThisFrame)
            ClearSlotContext();

        var hasPanel = TryGetPanelModel(out var panel);
        var hasSlot = TryGetSlotModel(out var slot);
        if (!hasPanel && !hasSlot)
            return;

        TryOpen(pluginRightClickConsumed, slotClickedThisFrame);

        var panelRows = hasPanel
            ? BuildPanelRows(panel, isEditing, importPage, exportPage, importNative, toggleEdit, toggleCollapse, closeOverlay)
            : new List<MenuActionRow>();
        var slotRows = hasSlot ? BuildSlotRows(slot, editSlot) : new List<MenuActionRow>();

        var labels = new List<string>(16);
        if (hasPanel)
        {
            labels.Add(panel.Header);
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

                if (hasPanel)
                {
                    ContextMenuItem.DrawHeader(panel.Header, style, rowHeight);
                    DrawRows(panelRows, style);
                }

                if (hasSlot)
                {
                    if (hasPanel)
                        ContextMenuItem.DrawSeparator(style);
                    ContextMenuItem.DrawHeader(slot.Name, style, rowHeight);
                    DrawRows(slotRows, style);
                }

                ImGui.EndPopup();
            }
            else
                _hasMenuRect = false;
        }

        if (!ImGui.IsPopupOpen(PopupId))
        {
            _hasMenuRect = false;
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

    private static void DrawRows(List<MenuActionRow> rows, ContextMenuStyleConfig style)
    {
        foreach (var row in rows)
        {
            if (ContextMenuItem.Draw(
                    row.Label,
                    row.Icon,
                    row.Id,
                    style,
                    row.TrailingLabel,
                    row.Enabled,
                    row.DisabledTooltip))
                row.OnClick();
        }
    }

    private static List<MenuActionRow> BuildPanelRows(
        PanelMenuModel model,
        bool isEditing,
        Action importPage,
        Action exportPage,
        Action<int> importNative,
        Action toggleEdit,
        Action toggleCollapse,
        Action closeOverlay)
    {
        var rows = new List<MenuActionRow>(8);
        if (model.ShowSettings)
            rows.Add(new(model.SettingsLabel, FontAwesomeIcon.Cog, "##eqpContextSettings", ToggleConfig));
        if (model.ShowImportPage)
            rows.Add(new(model.ImportPageLabel, FontAwesomeIcon.Download, "##eqpContextImportPage", importPage));
        if (model.ShowExportPage)
            rows.Add(new(model.ExportPageLabel, FontAwesomeIcon.Upload, "##eqpContextExportPage", exportPage));
        if (model.ShowImportNative)
        {
            rows.Add(new(model.ImportNativeLabel, FontAwesomeIcon.FileImport, "##eqpContextImportNative", () =>
            {
                NativeQuickPanelImportPopup.Open(onImported: importNative);
                ImGui.CloseCurrentPopup();
                _anchor = null;
            }));
        }

        if (model.ShowEdit)
            rows.Add(new(model.EditLabel, FontAwesomeIcon.Pen, "##eqpContextEdit", toggleEdit, TrailingLabel: isEditing ? "✓" : null));
        if (model.ShowCollapse)
            rows.Add(new(model.CollapseLabel, PanelCollapse.Icon, "##eqpContextCollapse", toggleCollapse));
        if (model.ShowClose)
            rows.Add(new(model.CloseLabel, FontAwesomeIcon.Times, "##eqpContextClose", closeOverlay));
        return rows;
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

    private static bool TryGetPanelModel(out PanelMenuModel model)
    {
        var items = Config.ContextMenuItems;
        if (!items.HasVisibleItems)
        {
            model = default;
            return false;
        }

        model = new PanelMenuModel(
            PluginServices.PluginInterface.Manifest.Name,
            items.IsSettingsVisible,
            items.IsImportPageVisible,
            items.IsExportPageVisible,
            items.IsImportNativeVisible,
            items.IsEditVisible,
            items.IsCollapseVisible,
            items.IsCloseVisible,
            T("contextMenu.settings"),
            T("contextMenu.importPage"),
            T("contextMenu.exportPage"),
            T("contextMenu.importNative"),
            T("contextMenu.edit"),
            PanelCollapse.Label,
            T("contextMenu.close"));
        return true;
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
        Action OnClick,
        string? TrailingLabel = null,
        bool Enabled = true,
        string? DisabledTooltip = null);

    private readonly record struct PanelMenuModel(
        string Header,
        bool ShowSettings,
        bool ShowImportPage,
        bool ShowExportPage,
        bool ShowImportNative,
        bool ShowEdit,
        bool ShowCollapse,
        bool ShowClose,
        string SettingsLabel,
        string ImportPageLabel,
        string ExportPageLabel,
        string ImportNativeLabel,
        string EditLabel,
        string CollapseLabel,
        string CloseLabel);

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
