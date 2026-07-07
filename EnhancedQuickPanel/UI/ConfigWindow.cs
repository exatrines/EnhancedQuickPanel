using ConfigWindowBase = ECommons.SimpleGui.ConfigWindow;
using Dalamud.Interface.Utility.Raii;
using EnhancedQuickPanel.Models;
using EnhancedQuickPanel.Services;
using MirageUI.Layout;
using MirageUI.Theme;

namespace EnhancedQuickPanel.UI;

/// <summary>Plugin configuration window hosting the Settings and Style tabs.</summary>
public sealed class ConfigWindow : ConfigWindowBase
{
    private static readonly string IconPath = Path.Combine(
        Svc.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty,
        "Assets",
        "EnhancedQuickPanel.png");

    private string _selectedTabId = string.Empty;

    public ConfigWindow()
    {
        MirageWindowDefaults.ApplyTo(this);
        Flags |= ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
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

    private ImRaii.ColorDisposable? _themeScope;

    public override void Draw() => GenericHelpers.Safe(DrawContent);

    private void DrawContent()
    {
        MirageUi.TwoColumn.Draw(CreateTwoColumnState(), DrawMainContent);
    }

    private MirageTwoColumnState CreateTwoColumnState() => new()
    {
        ShowSidebarHeader = true,
        ShowSidebarFooter = true,
        SidebarHeader = new MirageTwoColumnSidebarHeader
        {
            ImagePath = IconPath,
            ImageWidth = 48f,
            ImageHeight = 48f,
            Title = Svc.PluginInterface.Manifest.Name,
            Subtitle = $"v{Svc.PluginInterface.Manifest.AssemblyVersion} by {Svc.PluginInterface.Manifest.Author}",
        },
        SidebarFooterLinks =
        [
            new MirageTwoColumnSidebarFooterLink { Label = "GitHub", Url = "https://github.com/exatrines/EnhancedQuickPanel" },
            new MirageTwoColumnSidebarFooterLink { Label = "OFUSE", Url = "https://ofuse.me/exatrines" },
            new MirageTwoColumnSidebarFooterLink { Label = "Ko-fi", Url = "https://ko-fi.com/exatrines" },
        ],
        Entries =
        [
            new MirageTwoColumnEntry { Id = "settings", Label = T("config.tab.settings") },
            new MirageTwoColumnEntry { Id = "style", Label = T("config.tab.style") },
        ],
        SelectedId = _selectedTabId,
        OnSelectionChanged = id => _selectedTabId = id,
    };

    private void DrawMainContent()
    {
        switch (_selectedTabId)
        {
            case "settings":
                DrawSettingsPage();
                break;
            case "style":
                DrawStylePage();
                break;
        }
    }

    private void DrawSettingsPage()
    {
        MirageUi.SubHeader(T("config.quickPanel.title"));

        var replaceNative = C.DisplayMode == QuickPanelDisplayMode.PluginOnly;
        if (ImGui.Checkbox(T("config.quickPanel.replaceNative"), ref replaceNative))
        {
            QuickPanelDisplayCoordinator.SetDisplayMode(
                replaceNative ? QuickPanelDisplayMode.PluginOnly : QuickPanelDisplayMode.NativeOnly);
        }
        
        MirageUi.SubHeader(T("config.header.title"));
        MirageUi.Text(T("config.header.description"), MirageUi.Color.Secondary);

        var showPageSelectorPopup = C.ShowPageSelectorPopup ?? true;
        if (ImGui.Checkbox(T("config.header.showPageSelectorPopup"), ref showPageSelectorPopup))
        {
            C.ShowPageSelectorPopup = showPageSelectorPopup;
            EzConfig.Save();
        }

        var showEmptySlotBorder = C.ShowEmptySlotBorder ?? true;
        if (ImGui.Checkbox(T("config.header.showEmptySlotBorder"), ref showEmptySlotBorder))
        {
            C.ShowEmptySlotBorder = showEmptySlotBorder;
            EzConfig.Save();
        }

        var showEditButton = C.ShowEditButton;
        if (ImGui.Checkbox(T("config.header.showEditButton"), ref showEditButton))
        {
            C.ShowEditButton = showEditButton;
            EzConfig.Save();
        }

        DrawContextMenuItemsSettings();
    }

    private void DrawContextMenuItemsSettings()
    {
        MirageUi.SubHeader(T("config.contextMenuItems.title"));
        MirageUi.Text(T("config.contextMenuItems.description"), MirageUi.Color.Secondary);

        var items = C.ContextMenuItems;
        var changed = false;

        var showSettings = items.IsSettingsVisible;
        if (ImGui.Checkbox(T("contextMenu.settings"), ref showSettings))
        {
            items.ShowSettings = showSettings;
            changed = true;
        }

        var showImportPage = items.IsImportPageVisible;
        if (ImGui.Checkbox(T("contextMenu.importPage"), ref showImportPage))
        {
            items.ShowImportPage = showImportPage;
            changed = true;
        }

        var showExportPage = items.IsExportPageVisible;
        if (ImGui.Checkbox(T("contextMenu.exportPage"), ref showExportPage))
        {
            items.ShowExportPage = showExportPage;
            changed = true;
        }

        var showImportNative = items.IsImportNativeVisible;
        if (ImGui.Checkbox(T("contextMenu.importNative"), ref showImportNative))
        {
            items.ShowImportNative = showImportNative;
            changed = true;
        }

        var showEdit = items.IsEditVisible;
        if (ImGui.Checkbox(T("contextMenu.edit"), ref showEdit))
        {
            items.ShowEdit = showEdit;
            changed = true;
        }

        var showClose = items.IsCloseVisible;
        if (ImGui.Checkbox(T("contextMenu.close"), ref showClose))
        {
            items.ShowClose = showClose;
            changed = true;
        }

        if (changed)
            EzConfig.Save();
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
        if (ImGui.Button(T("common.export"), new Vector2(buttonWidth, 0f)))
            PanelStyleImportExport.ExportToClipboard();

        ImGui.SameLine();
        if (ImGui.Button(T("common.import"), new Vector2(buttonWidth, 0f)))
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
        if (ImGui.Button(applyLabel))
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
            EzConfig.Save();
    }

    private void DrawStyleLayoutSection()
    {
        MirageUi.SubHeader(T("config.style.layout"));

        var changed = false;

        var slotSize = C.SlotSize;
        if (ImGui.SliderFloat(T("config.style.slotSize"), ref slotSize, 28f, 64f))
        {
            C.SlotSize = slotSize;
            changed = true;
        }

        var slotPadding = C.SlotPadding;
        if (ImGui.SliderFloat(T("config.style.iconSpacing"), ref slotPadding, 0f, 32f))
        {
            C.SlotPadding = slotPadding;
            changed = true;
        }

        var windowPadding = C.WindowPadding;
        if (ImGui.SliderFloat(T("config.style.windowPadding"), ref windowPadding, 0f, 32f))
        {
            C.WindowPadding = windowPadding;
            changed = true;
        }

        SaveStyleIfChanged(changed);
    }

    private void DrawStyleWindowSection()
    {
        MirageUi.SubHeader(T("config.style.window"));

        var changed = false;

        MirageUi.Text(T("config.style.windowNormal"), MirageUi.Color.Secondary);

        var windowBg = C.WindowBgColor;
        if (ImGui.ColorEdit4(T("config.style.windowBg"), ref windowBg, ImGuiColorEditFlags.AlphaBar))
        {
            C.SetWindowBgColor(windowBg);
            changed = true;
        }

        var windowBorder = C.WindowBorderColor;
        if (ImGui.ColorEdit4(T("config.style.windowBorder"), ref windowBorder, ImGuiColorEditFlags.AlphaBar))
        {
            C.SetWindowBorderColor(windowBorder);
            changed = true;
        }

        var windowBorderThickness = C.WindowBorderThickness;
        if (ImGui.SliderFloat(
                $"{T("config.style.borderThickness")}##eqpWindowBorder",
                ref windowBorderThickness,
                0f,
                8f))
        {
            C.WindowBorderThickness = windowBorderThickness;
            changed = true;
        }

        MirageUi.Text(T("config.style.windowEditMode"), MirageUi.Color.Secondary);

        var editModeWindowBg = C.EditModeWindowBgColor;
        if (ImGui.ColorEdit4(
                T("config.style.editModeBg"),
                ref editModeWindowBg,
                ImGuiColorEditFlags.AlphaBar))
        {
            C.SetEditModeWindowBgColor(editModeWindowBg);
            changed = true;
        }

        var editModeWindowBorder = C.EditModeWindowBorderColor;
        if (ImGui.ColorEdit4(
                T("config.style.editModeBorder"),
                ref editModeWindowBorder,
                ImGuiColorEditFlags.AlphaBar))
        {
            C.SetEditModeWindowBorderColor(editModeWindowBorder);
            changed = true;
        }

        var editModeWindowBorderThickness = C.EditModeWindowBorderThickness;
        if (ImGui.SliderFloat(
                $"{T("config.style.editBorderThickness")}##eqpEditModeWindowBorder",
                ref editModeWindowBorderThickness,
                0f,
                8f))
        {
            C.EditModeWindowBorderThickness = editModeWindowBorderThickness;
            changed = true;
        }

        SaveStyleIfChanged(changed);
    }

    private void DrawStyleSlotSection()
    {
        MirageUi.SubHeader(T("config.style.slots"));

        var changed = false;

        var slotBg = C.SlotBgColor;
        if (ImGui.ColorEdit4(T("config.style.slotBg"), ref slotBg, ImGuiColorEditFlags.AlphaBar))
        {
            C.SetSlotBgColor(slotBg);
            changed = true;
        }

        var slotDropTargetBg = C.SlotDropTargetColor;
        if (ImGui.ColorEdit4(
                T("config.style.dropTargetHighlight"),
                ref slotDropTargetBg,
                ImGuiColorEditFlags.AlphaBar))
        {
            C.SlotDropTargetRed = slotDropTargetBg.X;
            C.SlotDropTargetGreen = slotDropTargetBg.Y;
            C.SlotDropTargetBlue = slotDropTargetBg.Z;
            C.SlotDropTargetAlpha = slotDropTargetBg.W;
            changed = true;
        }

        SaveStyleIfChanged(changed);

        SaveStyleIfChanged(DrawOverlayLabelStyles());
    }

    private void DrawStyleTooltipSection()
    {
        MirageUi.SubHeader(T("config.style.tooltip"));

        var changed = false;

        var tooltipBg = C.TooltipBgColor;
        if (ImGui.ColorEdit4(T("config.style.tooltipBg"), ref tooltipBg, ImGuiColorEditFlags.AlphaBar))
        {
            C.SetTooltipBgColor(tooltipBg);
            changed = true;
        }

        var tooltipText = C.TooltipTextColor;
        if (ImGui.ColorEdit4(T("config.style.tooltipText"), ref tooltipText, ImGuiColorEditFlags.AlphaBar))
        {
            C.SetTooltipTextColor(tooltipText);
            changed = true;
        }

        var tooltipPadding = C.TooltipPadding;
        if (ImGui.SliderFloat(T("config.style.tooltipPadding"), ref tooltipPadding, 0f, 32f))
        {
            C.TooltipPadding = tooltipPadding;
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
        var style = C.ContextMenu;
        style.EnsureDefaults();
        var changed = false;

        var padding = style.Padding;
        if (ImGui.SliderFloat($"{T("config.contextMenu.padding")}##eqpContextMenuPadding", ref padding, 0f, 16f))
        {
            style.Padding = padding;
            changed = true;
        }

        var bgColor = style.BgColor;
        if (ImGui.ColorEdit4($"{T("config.contextMenu.bg")}##eqpContextMenuBg", ref bgColor, ImGuiColorEditFlags.AlphaBar))
        {
            style.SetBgColor(bgColor);
            changed = true;
        }

        var textColor = style.TextColor;
        if (ImGui.ColorEdit4($"{T("config.contextMenu.text")}##eqpContextMenuText", ref textColor, ImGuiColorEditFlags.AlphaBar))
        {
            style.SetTextColor(textColor);
            changed = true;
        }

        var textHoverColor = style.TextHoverColor;
        if (ImGui.ColorEdit4(
                $"{T("config.contextMenu.textHover")}##eqpContextMenuTextHover",
                ref textHoverColor,
                ImGuiColorEditFlags.AlphaBar))
        {
            style.SetTextHoverColor(textHoverColor);
            changed = true;
        }

        var buttonBgColor = style.ButtonBgColor;
        if (ImGui.ColorEdit4(
                $"{T("config.contextMenu.buttonBg")}##eqpContextMenuButtonBg",
                ref buttonBgColor,
                ImGuiColorEditFlags.AlphaBar))
        {
            style.SetButtonBgColor(buttonBgColor);
            changed = true;
        }

        var buttonBgHoverColor = style.ButtonBgHoverColor;
        if (ImGui.ColorEdit4(
                $"{T("config.contextMenu.buttonBgHover")}##eqpContextMenuButtonBgHover",
                ref buttonBgHoverColor,
                ImGuiColorEditFlags.AlphaBar))
        {
            style.SetButtonBgHoverColor(buttonBgHoverColor);
            changed = true;
        }

        if (changed)
            EzConfig.Save();
    }

    private void DrawPanelUiSettings()
    {
        var style = C.PanelUi;
        var changed = false;

        MirageUi.Text(T("config.style.panelUiDescription"), MirageUi.Color.Secondary);

        var textColor = style.TextColor;
        if (ImGui.ColorEdit4($"{T("config.style.panelUiText")}##eqpPanelUiText", ref textColor, ImGuiColorEditFlags.AlphaBar))
        {
            style.SetTextColor(textColor);
            changed = true;
        }

        var textHoverColor = style.TextHoverColor;
        if (ImGui.ColorEdit4(
                $"{T("config.style.panelUiTextHover")}##eqpPanelUiTextHover",
                ref textHoverColor,
                ImGuiColorEditFlags.AlphaBar))
        {
            style.SetTextHoverColor(textHoverColor);
            changed = true;
        }

        var buttonBgColor = style.ButtonBgColor;
        if (ImGui.ColorEdit4(
                $"{T("config.style.panelUiButtonBg")}##eqpPanelUiButtonBg",
                ref buttonBgColor,
                ImGuiColorEditFlags.AlphaBar))
        {
            style.SetButtonBgColor(buttonBgColor);
            changed = true;
        }

        var buttonBgHoverColor = style.ButtonBgHoverColor;
        if (ImGui.ColorEdit4(
                $"{T("config.style.panelUiButtonBgHover")}##eqpPanelUiButtonBgHover",
                ref buttonBgHoverColor,
                ImGuiColorEditFlags.AlphaBar))
        {
            style.SetButtonBgHoverColor(buttonBgHoverColor);
            changed = true;
        }

        if (changed)
            EzConfig.Save();
    }

    private const ImGuiColorEditFlags OverlayColorPickerFlags = ImGuiColorEditFlags.AlphaBar;

    private static bool DrawOverlayLabelStyles()
    {
        var changed = false;
        changed |= DrawOverlayLabelStyleBlock("cd", T("config.style.overlayCooldown"), C.CooldownLabelStyle);
        changed |= DrawOverlayLabelStyleBlock("ch", T("config.style.overlayCharge"), C.ChargeLabelStyle);
        changed |= DrawOverlayLabelStyleBlock("qt", T("config.style.overlayQuantity"), C.QuantityLabelStyle);
        changed |= DrawOverlayLabelStyleBlock("mg", T("config.style.overlayMacroGear"), C.MacroGearLabelStyle, isLast: true);
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
        if (ImGui.SliderFloat($"{T("config.style.overlayTextSize")}##{id}TextSize", ref textSize, 0.5f, 2f))
        {
            style.TextSizeScale = textSize;
            changed = true;
        }

        var textColor = style.TextColor;
        if (ImGui.ColorEdit4(
                $"{T("config.style.overlayTextColor")}##{id}TextColor",
                ref textColor,
                OverlayColorPickerFlags))
        {
            style.SetTextColor(textColor);
            changed = true;
        }

        var edgeThickness = style.EdgeThickness;
        if (ImGui.SliderFloat(
                $"{T("config.style.overlayEdgeThickness")}##{id}EdgeThickness",
                ref edgeThickness,
                0f,
                4f))
        {
            style.EdgeThickness = edgeThickness;
            changed = true;
        }

        var edgeColor = style.EdgeColor;
        if (ImGui.ColorEdit4(
                $"{T("config.style.overlayEdgeColor")}##{id}EdgeColor",
                ref edgeColor,
                OverlayColorPickerFlags))
        {
            style.SetEdgeColor(edgeColor);
            changed = true;
        }

        return changed;
    }
}
