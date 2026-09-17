using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using EnhancedQuickPanel.Models;
using EnhancedQuickPanel.Services;
using MirageUI.Layout;
using MirageUI.Theme;

namespace EnhancedQuickPanel.UI;

// Settings and Style tabs.
public sealed class ConfigurationWindow : Window
{
    private static string IconPath => Path.Combine(
        PluginServices.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty,
        "Data",
        "plugin-icon.png");

    private string _selectedTabId = "settings";
    private ImRaii.ColorDisposable? _themeScope;

    public ConfigurationWindow()
        : base("Enhanced Quick Panel###eqpConfig")
    {
        MirageWindowDefaults.ApplyTo(this);
        Flags |= ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(200f, 200f),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public override void PreDraw()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        MirageTheme.EnsureDefaultsCaptured();
        _themeScope = MirageTheme.PushCustom(MirageTheme.ResolveAppliedColors());
    }

    public override void PostDraw()
    {
        MirageTheme.Pop(_themeScope);
        _themeScope = null;
        ImGui.PopStyleVar();
    }

    public override void Draw() => AddonAccess.Safe(DrawContent);

    private void DrawContent()
    {
        MirageUi.TwoColumn.Draw(CreateTwoColumnState(), DrawMainContent);
    }

    private MirageTwoColumnState CreateTwoColumnState() => new()
    {
        ShowSidebarHeader = true,
        ShowSidebarFooter = false,
        SidebarHeader = new MirageTwoColumnSidebarHeader
        {
            ImagePath = IconPath,
            ImageWidth = 48f,
            ImageHeight = 48f,
            Title = PluginServices.PluginInterface.Manifest.Name,
            Subtitle = $"v{PluginServices.PluginInterface.Manifest.AssemblyVersion} by {PluginServices.PluginInterface.Manifest.Author}",
        },
        Entries =
        [
            new MirageTwoColumnEntry { Id = "settings", Label = T("config.tab.settings") },
            new MirageTwoColumnEntry { Id = "style", Label = T("config.tab.style") },
        ],
        SelectedId = string.IsNullOrEmpty(_selectedTabId) ? "settings" : _selectedTabId,
        AllowDeselect = false,
        OnSelectionChanged = id => _selectedTabId = id,
    };

    private void DrawMainContent()
    {
        switch (_selectedTabId)
        {
            case "style":
                DrawStylePage();
                break;
            default:
                DrawSettingsPage();
                break;
        }
    }

    private void DrawSettingsPage()
    {
        MirageUi.SubHeader(T("config.quickPanel.title"));

        var replaceNative = Config.DisplayMode == PanelDisplayMode.PluginOnly;
        if (MirageUi.Checkbox(T("config.quickPanel.replaceNative"), ref replaceNative))
        {
            PanelDisplayCoordinator.SetDisplayMode(
                replaceNative ? PanelDisplayMode.PluginOnly : PanelDisplayMode.NativeOnly);
        }

        DrawLayoutSettings();

        MirageUi.SubHeader(T("config.header.title"));
        MirageUi.Text(T("config.header.description"), MirageUi.Color.Secondary);

        var showPageSelectorPopup = Config.ShowPageSelectorPopup ?? true;
        if (MirageUi.Checkbox(T("config.header.showPageSelectorPopup"), ref showPageSelectorPopup))
        {
            Config.ShowPageSelectorPopup = showPageSelectorPopup;
            Config.Save();
        }

        var showEmptySlotBorder = Config.ShowEmptySlotBorder ?? true;
        if (MirageUi.Checkbox(T("config.header.showEmptySlotBorder"), ref showEmptySlotBorder))
        {
            Config.ShowEmptySlotBorder = showEmptySlotBorder;
            Config.Save();
        }

        var showCollapseButton = Config.ShowCollapseButton;
        if (MirageUi.Checkbox(T("config.header.showCollapseButton"), ref showCollapseButton))
        {
            Config.ShowCollapseButton = showCollapseButton;
            Config.Save();
        }

        var showEditButton = Config.ShowEditButton;
        if (MirageUi.Checkbox(T("config.header.showEditButton"), ref showEditButton))
        {
            Config.ShowEditButton = showEditButton;
            Config.Save();
        }

        MirageUi.SubHeader(T("config.pluginShortcut.title"));
        var middleClickToggle = Config.PluginMiddleClickTogglesEnabled;
        if (MirageUi.Checkbox(T("config.pluginShortcut.middleClickToggle"), ref middleClickToggle))
        {
            Config.PluginMiddleClickTogglesEnabled = middleClickToggle;
            Config.Save();
        }

        var rightClickOpenSettings = !Config.PluginRightClickOpensSlotMenu;
        if (MirageUi.Checkbox(T("config.pluginShortcut.rightClickOpenSettings"), ref rightClickOpenSettings))
        {
            Config.PluginRightClickOpensSlotMenu = !rightClickOpenSettings;
            Config.Save();
        }

        DrawContextMenuItemsSettings();
    }

    private static readonly (int Columns, int Rows)[] LayoutOptions =
    [
        (1, 1),
        (1, 2),
        (2, 1),
        (2, 2),
    ];

    private void DrawLayoutSettings()
    {
        MirageUi.SubHeader(T("config.layout.title"));
        MirageUi.Text(T("config.layout.description"), MirageUi.Color.Secondary);

        Config.EnsureDefaults();
        var preview = FormatLayoutLabel(Config.LayoutBlockColumns, Config.LayoutBlockRows);
        ImGui.SetNextItemWidth(120f);
        if (ImGui.BeginCombo(T("config.layout.size") + "##eqpLayoutSize", preview))
        {
            foreach (var option in LayoutOptions)
            {
                var selected = option.Columns == Config.LayoutBlockColumns && option.Rows == Config.LayoutBlockRows;
                if (ImGui.Selectable(FormatLayoutLabel(option.Columns, option.Rows), selected)
                    && !selected
                    && !PanelLayoutApply.TryApply(option.Columns, option.Rows, out var error))
                    Notifications.Warning(error);
            }

            ImGui.EndCombo();
        }
    }

    private static string FormatLayoutLabel(int blockColumns, int blockRows) =>
        $"{PanelLayout.Columns(blockColumns)}×{PanelLayout.Rows(blockRows)}";

    private void DrawContextMenuItemsSettings()
    {
        MirageUi.SubHeader(T("config.contextMenuItems.title"));
        MirageUi.Text(T("config.contextMenuItems.description"), MirageUi.Color.Secondary);

        var items = Config.ContextMenuItems;
        var changed = false;
        void Checkbox(string key, bool visible, Action<bool> assign)
        {
            var value = visible;
            if (MirageUi.Checkbox(T(key), ref value))
            {
                assign(value);
                changed = true;
            }
        }

        Checkbox("contextMenu.settings", items.IsSettingsVisible, value => items.ShowSettings = value);
        Checkbox("contextMenu.importPage", items.IsImportPageVisible, value => items.ShowImportPage = value);
        Checkbox("contextMenu.exportPage", items.IsExportPageVisible, value => items.ShowExportPage = value);
        Checkbox("contextMenu.importNative", items.IsImportNativeVisible, value => items.ShowImportNative = value);
        Checkbox("contextMenu.edit", items.IsEditVisible, value => items.ShowEdit = value);
        Checkbox("contextMenu.collapse", items.IsCollapseVisible, value => items.ShowCollapse = value);
        Checkbox("contextMenu.close", items.IsCloseVisible, value => items.ShowClose = value);

        if (changed)
            Config.Save();
    }

    private void DrawStylePage()
    {
        DrawStyleImportExportSection();
        DrawStyleLayoutSection();
        DrawStyleWindowSection();
        DrawStyleSlotSection();
        DrawStyleTooltipSection();
        DrawStylePanelUiSection();
        DrawStyleContextMenuSection();
    }

    private int _selectedStylePresetIndex;

    private void DrawStyleImportExportSection()
    {
        MirageUi.SubHeader(T("config.style.importExport"));

        MirageUi.Text(T("config.style.importExportDescription"), MirageUi.Color.Secondary);

        var buttonWidth = CalcTwoButtonWidth(T("common.export"), T("common.import"));
        if (MirageUi.PrimaryButton(T("common.export"), width: buttonWidth))
            PanelStyleImportExport.ExportToClipboard();

        ImGui.SameLine();
        if (MirageUi.SecondaryButton(T("common.import"), width: buttonWidth))
            PanelStyleImportExport.ImportFromClipboard();

        DrawStylePresetRow();
    }

    private void DrawStylePresetRow()
    {
        var presets = PanelStylePresets.All;
        if (presets.Length == 0)
        {
            MirageUi.Text(T("config.style.presetUnavailable"), MirageUi.Color.Warning);
            return;
        }

        MirageUi.Text(T("config.style.presetDescription"), MirageUi.Color.Secondary);

        _selectedStylePresetIndex = Math.Clamp(_selectedStylePresetIndex, 0, presets.Length - 1);
        var selectedPreset = presets[_selectedStylePresetIndex];

        var applyLabel = T("config.style.presetImport");

        ImGui.SetNextItemWidth(200f);
        if (ImGui.BeginCombo("##eqpStylePreset", PanelStylePresets.GetLabel(selectedPreset)))
        {
            for (var i = 0; i < presets.Length; i++)
            {
                var preset = presets[i];
                if (ImGui.Selectable(PanelStylePresets.GetLabel(preset), i == _selectedStylePresetIndex))
                    _selectedStylePresetIndex = i;
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        if (MirageUi.PrimaryButton(applyLabel))
            PanelStylePresets.ApplyFromUi(_selectedStylePresetIndex);
    }

    private static float CalcTwoButtonWidth(string a, string b)
    {
        var framePadding = ImGui.GetStyle().FramePadding.X * 2f;
        var widest = Math.Max(ImGui.CalcTextSize(a).X, ImGui.CalcTextSize(b).X);
        return widest + framePadding + 12f;
    }

    private static void SaveStyleIfChanged(bool changed)
    {
        if (changed)
            Config.Save();
    }

    private void DrawStyleLayoutSection()
    {
        MirageUi.SubHeader(T("config.style.layout"));

        var changed = false;

        var slotSize = Config.SlotSize;
        if (MirageUi.SliderFloat(T("config.style.slotSize"), ref slotSize, 28f, 64f))
        {
            Config.SlotSize = slotSize;
            changed = true;
        }

        var slotPadding = Config.SlotPadding;
        if (MirageUi.SliderFloat(T("config.style.iconSpacing"), ref slotPadding, 0f, 32f))
        {
            Config.SlotPadding = slotPadding;
            changed = true;
        }

        var windowPadding = Config.WindowPadding;
        if (MirageUi.SliderFloat(T("config.style.windowPadding"), ref windowPadding, 0f, 32f))
        {
            Config.WindowPadding = windowPadding;
            changed = true;
        }

        SaveStyleIfChanged(changed);
    }

    private void DrawStyleWindowSection()
    {
        MirageUi.SubHeader(T("config.style.window"));

        var changed = false;

        MirageUi.Text(T("config.style.windowNormal"), MirageUi.Color.Secondary);

        var windowBg = Config.WindowBgColor;
        if (MirageUi.ColorEdit4(T("config.style.windowBg"), ref windowBg))
        {
            Config.SetWindowBgColor(windowBg);
            changed = true;
        }

        var windowBorder = Config.WindowBorderColor;
        if (MirageUi.ColorEdit4(T("config.style.windowBorder"), ref windowBorder))
        {
            Config.SetWindowBorderColor(windowBorder);
            changed = true;
        }

        var windowBorderThickness = Config.WindowBorderThickness;
        if (MirageUi.SliderFloat(
                T("config.style.borderThickness"),
                ref windowBorderThickness,
                0f,
                8f,
                id: "eqpWindowBorder"))
        {
            Config.WindowBorderThickness = windowBorderThickness;
            changed = true;
        }

        MirageUi.Text(T("config.style.windowEditMode"), MirageUi.Color.Secondary);

        var editModeWindowBg = Config.EditModeWindowBgColor;
        if (MirageUi.ColorEdit4(T("config.style.editModeBg"), ref editModeWindowBg))
        {
            Config.SetEditModeWindowBgColor(editModeWindowBg);
            changed = true;
        }

        var editModeWindowBorder = Config.EditModeWindowBorderColor;
        if (MirageUi.ColorEdit4(T("config.style.editModeBorder"), ref editModeWindowBorder))
        {
            Config.SetEditModeWindowBorderColor(editModeWindowBorder);
            changed = true;
        }

        var editModeWindowBorderThickness = Config.EditModeWindowBorderThickness;
        if (MirageUi.SliderFloat(
                T("config.style.editBorderThickness"),
                ref editModeWindowBorderThickness,
                0f,
                8f,
                id: "eqpEditModeWindowBorder"))
        {
            Config.EditModeWindowBorderThickness = editModeWindowBorderThickness;
            changed = true;
        }

        SaveStyleIfChanged(changed);
    }

    private void DrawStyleSlotSection()
    {
        MirageUi.SubHeader(T("config.style.slots"));

        var changed = false;

        var slotBg = Config.SlotBgColor;
        if (MirageUi.ColorEdit4(T("config.style.slotBg"), ref slotBg))
        {
            Config.SetSlotBgColor(slotBg);
            changed = true;
        }

        var slotDropTargetBg = Config.SlotDropTargetColor;
        if (MirageUi.ColorEdit4(T("config.style.dropTargetHighlight"), ref slotDropTargetBg))
        {
            Config.SlotDropTargetRed = slotDropTargetBg.X;
            Config.SlotDropTargetGreen = slotDropTargetBg.Y;
            Config.SlotDropTargetBlue = slotDropTargetBg.Z;
            Config.SlotDropTargetAlpha = slotDropTargetBg.W;
            changed = true;
        }

        SaveStyleIfChanged(changed);

        SaveStyleIfChanged(DrawOverlayLabelStyles());
    }

    private void DrawStyleTooltipSection()
    {
        MirageUi.SubHeader(T("config.style.tooltip"));

        var changed = false;

        var tooltipBg = Config.TooltipBgColor;
        if (MirageUi.ColorEdit4(T("config.style.tooltipBg"), ref tooltipBg))
        {
            Config.SetTooltipBgColor(tooltipBg);
            changed = true;
        }

        var tooltipText = Config.TooltipTextColor;
        if (MirageUi.ColorEdit4(T("config.style.tooltipText"), ref tooltipText))
        {
            Config.SetTooltipTextColor(tooltipText);
            changed = true;
        }

        var tooltipPadding = Config.TooltipPadding;
        if (MirageUi.SliderFloat(T("config.style.tooltipPadding"), ref tooltipPadding, 0f, 32f))
        {
            Config.TooltipPadding = tooltipPadding;
            changed = true;
        }

        SaveStyleIfChanged(changed);
    }

    private void DrawStylePanelUiSection()
    {
        MirageUi.SubHeader(T("config.style.panelUi"));
        DrawPanelUiSettings();
    }

    private void DrawStyleContextMenuSection()
    {
        MirageUi.SubHeader(T("config.style.contextMenu"));
        DrawContextMenuSettings();
    }

    private void DrawContextMenuSettings()
    {
        var style = Config.ContextMenu;
        style.EnsureDefaults();
        var changed = false;

        var padding = style.Padding;
        if (MirageUi.SliderFloat(T("config.contextMenu.padding"), ref padding, 0f, 16f, id: "eqpContextMenuPadding"))
        {
            style.Padding = padding;
            changed = true;
        }

        var bgColor = style.BgColor;
        if (MirageUi.ColorEdit4(T("config.contextMenu.bg"), ref bgColor, id: "eqpContextMenuBg"))
        {
            style.SetBgColor(bgColor);
            changed = true;
        }

        var textColor = style.TextColor;
        if (MirageUi.ColorEdit4(T("config.contextMenu.text"), ref textColor, id: "eqpContextMenuText"))
        {
            style.SetTextColor(textColor);
            changed = true;
        }

        var textHoverColor = style.TextHoverColor;
        if (MirageUi.ColorEdit4(T("config.contextMenu.textHover"), ref textHoverColor, id: "eqpContextMenuTextHover"))
        {
            style.SetTextHoverColor(textHoverColor);
            changed = true;
        }

        var buttonBgColor = style.ButtonBgColor;
        if (MirageUi.ColorEdit4(T("config.contextMenu.buttonBg"), ref buttonBgColor, id: "eqpContextMenuButtonBg"))
        {
            style.SetButtonBgColor(buttonBgColor);
            changed = true;
        }

        var buttonBgHoverColor = style.ButtonBgHoverColor;
        if (MirageUi.ColorEdit4(
                T("config.contextMenu.buttonBgHover"),
                ref buttonBgHoverColor,
                id: "eqpContextMenuButtonBgHover"))
        {
            style.SetButtonBgHoverColor(buttonBgHoverColor);
            changed = true;
        }

        if (changed)
            Config.Save();
    }

    private void DrawPanelUiSettings()
    {
        var style = Config.PanelUi;
        var changed = false;

        MirageUi.Text(T("config.style.panelUiDescription"), MirageUi.Color.Secondary);

        var textColor = style.TextColor;
        if (MirageUi.ColorEdit4(T("config.style.panelUiText"), ref textColor, id: "eqpPanelUiText"))
        {
            style.SetTextColor(textColor);
            changed = true;
        }

        var textHoverColor = style.TextHoverColor;
        if (MirageUi.ColorEdit4(T("config.style.panelUiTextHover"), ref textHoverColor, id: "eqpPanelUiTextHover"))
        {
            style.SetTextHoverColor(textHoverColor);
            changed = true;
        }

        var buttonBgColor = style.ButtonBgColor;
        if (MirageUi.ColorEdit4(T("config.style.panelUiButtonBg"), ref buttonBgColor, id: "eqpPanelUiButtonBg"))
        {
            style.SetButtonBgColor(buttonBgColor);
            changed = true;
        }

        var buttonBgHoverColor = style.ButtonBgHoverColor;
        if (MirageUi.ColorEdit4(
                T("config.style.panelUiButtonBgHover"),
                ref buttonBgHoverColor,
                id: "eqpPanelUiButtonBgHover"))
        {
            style.SetButtonBgHoverColor(buttonBgHoverColor);
            changed = true;
        }

        if (changed)
            Config.Save();
    }

    private const ImGuiColorEditFlags OverlayColorPickerFlags = ImGuiColorEditFlags.AlphaBar;

    private static bool DrawOverlayLabelStyles()
    {
        var changed = false;
        changed |= DrawOverlayLabelStyleBlock("cd", T("config.style.overlayCooldown"), Config.CooldownLabelStyle);
        changed |= DrawOverlayLabelStyleBlock("ch", T("config.style.overlayCharge"), Config.ChargeLabelStyle);
        changed |= DrawOverlayLabelStyleBlock("qt", T("config.style.overlayQuantity"), Config.QuantityLabelStyle);
        changed |= DrawOverlayLabelStyleBlock("mg", T("config.style.overlayMacroGear"), Config.MacroGearLabelStyle, isLast: true);
        return changed;
    }

    private static bool DrawOverlayLabelStyleBlock(
        string id,
        string title,
        OverlayLabelStyleConfig style,
        bool isLast = false)
    {
        var changed = false;

        MirageUi.Text(title, MirageUi.Color.Secondary);

        var textSize = style.TextSizeScale;
        if (MirageUi.SliderFloat(T("config.style.overlayTextSize"), ref textSize, 0.5f, 2f, id: $"{id}TextSize"))
        {
            style.TextSizeScale = textSize;
            changed = true;
        }

        var textColor = style.TextColor;
        if (MirageUi.ColorEdit4(
                T("config.style.overlayTextColor"),
                ref textColor,
                OverlayColorPickerFlags,
                id: $"{id}TextColor"))
        {
            style.SetTextColor(textColor);
            changed = true;
        }

        var edgeThickness = style.EdgeThickness;
        if (MirageUi.SliderFloat(
                T("config.style.overlayEdgeThickness"),
                ref edgeThickness,
                0f,
                4f,
                id: $"{id}EdgeThickness"))
        {
            style.EdgeThickness = edgeThickness;
            changed = true;
        }

        var edgeColor = style.EdgeColor;
        if (MirageUi.ColorEdit4(
                T("config.style.overlayEdgeColor"),
                ref edgeColor,
                OverlayColorPickerFlags,
                id: $"{id}EdgeColor"))
        {
            style.SetEdgeColor(edgeColor);
            changed = true;
        }

        return changed;
    }
}
