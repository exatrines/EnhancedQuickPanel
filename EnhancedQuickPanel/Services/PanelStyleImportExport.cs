using System.Text.Json;
using System.Text.Json.Serialization;
using EnhancedQuickPanel.Models;
using ECommons.WindowsFormsReflector;

namespace EnhancedQuickPanel.Services;

/// <summary>Serializes and deserializes style settings for clipboard sharing.</summary>
internal static class PanelStyleImportExport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void ExportToClipboard()
    {
        try
        {
            C.EnsureDefaults();
            Winforms.Clipboard.SetText(Export(C));
            Notify.Success(T("panelStyle.exportSuccess"));
        }
        catch (Exception ex)
        {
            Notify.Error(T("panelStyle.exportFailed", ex.Message));
        }
    }

    public static void ImportFromClipboard()
    {
        try
        {
            var json = Winforms.Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(json))
            {
                Notify.Warning(T("panelStyle.error.noClipboard"));
                return;
            }

            if (!TryImport(json, C, out var error))
            {
                Notify.Warning(error);
                return;
            }

            C.EnsureDefaults();
            EzConfig.Save();
            Notify.Success(T("panelStyle.importSuccess"));
        }
        catch (Exception ex)
        {
            Notify.Error(T("panelStyle.importFailed", ex.Message));
        }
    }

    public static string Export(Configuration config)
    {
        config.EnsureDefaults();
        return JsonSerializer.Serialize(PanelStyleSnapshot.From(config), JsonOptions);
    }

    public static bool TryImport(string json, Configuration config, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = T("panelStyle.error.emptyJson");
            return false;
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<PanelStyleSnapshot>(json, JsonOptions);
            if (snapshot == null)
            {
                error = T("panelStyle.error.loadFailed");
                return false;
            }

            if (snapshot.Version == 0)
                snapshot.Version = PanelStyleSnapshot.CurrentVersion;

            if (snapshot.Version != PanelStyleSnapshot.CurrentVersion)
            {
                error = T("panelStyle.error.unsupportedVersion", snapshot.Version);
                return false;
            }

            snapshot.ApplyTo(config);
            return true;
        }
        catch (JsonException ex)
        {
            error = T("panelStyle.error.invalidJson", ex.Message);
            return false;
        }
    }

    /// <summary>Serializable snapshot of style settings.</summary>
    internal sealed class PanelStyleSnapshot
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;

        public float SlotSize { get; set; }

        public float SlotPadding { get; set; }

        public float WindowPadding { get; set; }

        public float WindowBgRed { get; set; }

        public float WindowBgGreen { get; set; }

        public float WindowBgBlue { get; set; }

        public float WindowBgAlpha { get; set; }

        public float EditModeWindowBgRed { get; set; }

        public float EditModeWindowBgGreen { get; set; }

        public float EditModeWindowBgBlue { get; set; }

        public float EditModeWindowBgAlpha { get; set; }

        public float WindowBorderRed { get; set; }

        public float WindowBorderGreen { get; set; }

        public float WindowBorderBlue { get; set; }

        public float WindowBorderAlpha { get; set; }

        public float WindowBorderThickness { get; set; }

        public float EditModeWindowBorderRed { get; set; }

        public float EditModeWindowBorderGreen { get; set; }

        public float EditModeWindowBorderBlue { get; set; }

        public float EditModeWindowBorderAlpha { get; set; }

        public float EditModeWindowBorderThickness { get; set; }

        public float SlotBgRed { get; set; }

        public float SlotBgGreen { get; set; }

        public float SlotBgBlue { get; set; }

        public float SlotBgAlpha { get; set; }

        public float SlotDropTargetRed { get; set; }

        public float SlotDropTargetGreen { get; set; }

        public float SlotDropTargetBlue { get; set; }

        public float SlotDropTargetAlpha { get; set; }

        public float SelectedSlotBorderRed { get; set; }

        public float SelectedSlotBorderGreen { get; set; }

        public float SelectedSlotBorderBlue { get; set; }

        public float SelectedSlotBorderAlpha { get; set; }

        public float TooltipBgRed { get; set; }

        public float TooltipBgGreen { get; set; }

        public float TooltipBgBlue { get; set; }

        public float TooltipBgAlpha { get; set; }

        public float TooltipTextRed { get; set; }

        public float TooltipTextGreen { get; set; }

        public float TooltipTextBlue { get; set; }

        public float TooltipTextAlpha { get; set; }

        public float TooltipPadding { get; set; }

        public ContextMenuStyleConfig ContextMenu { get; set; } = ContextMenuStyleConfig.CreateDefault();

        public PanelUiStyleConfig PanelUi { get; set; } = PanelUiStyleConfig.CreateDefault();

        public OverlayLabelStyleConfig CooldownLabelStyle { get; set; } =
            OverlayLabelStyleConfig.CreateDefaultText();

        public OverlayLabelStyleConfig ChargeLabelStyle { get; set; } =
            OverlayLabelStyleConfig.CreateDefaultText();

        public OverlayLabelStyleConfig QuantityLabelStyle { get; set; } =
            OverlayLabelStyleConfig.CreateDefaultText();

        public OverlayLabelStyleConfig MacroGearLabelStyle { get; set; } =
            OverlayLabelStyleConfig.CreateDefaultMacroGear();

        public static PanelStyleSnapshot From(Configuration config) =>
            new()
            {
                SlotSize = config.SlotSize,
                SlotPadding = config.SlotPadding,
                WindowPadding = config.WindowPadding,
                WindowBgRed = config.WindowBgRed,
                WindowBgGreen = config.WindowBgGreen,
                WindowBgBlue = config.WindowBgBlue,
                WindowBgAlpha = config.WindowBgAlpha,
                EditModeWindowBgRed = config.EditModeWindowBgRed,
                EditModeWindowBgGreen = config.EditModeWindowBgGreen,
                EditModeWindowBgBlue = config.EditModeWindowBgBlue,
                EditModeWindowBgAlpha = config.EditModeWindowBgAlpha,
                WindowBorderRed = config.WindowBorderRed,
                WindowBorderGreen = config.WindowBorderGreen,
                WindowBorderBlue = config.WindowBorderBlue,
                WindowBorderAlpha = config.WindowBorderAlpha,
                WindowBorderThickness = config.WindowBorderThickness,
                EditModeWindowBorderRed = config.EditModeWindowBorderRed,
                EditModeWindowBorderGreen = config.EditModeWindowBorderGreen,
                EditModeWindowBorderBlue = config.EditModeWindowBorderBlue,
                EditModeWindowBorderAlpha = config.EditModeWindowBorderAlpha,
                EditModeWindowBorderThickness = config.EditModeWindowBorderThickness,
                SlotBgRed = config.SlotBgRed,
                SlotBgGreen = config.SlotBgGreen,
                SlotBgBlue = config.SlotBgBlue,
                SlotBgAlpha = config.SlotBgAlpha,
                SlotDropTargetRed = config.SlotDropTargetRed,
                SlotDropTargetGreen = config.SlotDropTargetGreen,
                SlotDropTargetBlue = config.SlotDropTargetBlue,
                SlotDropTargetAlpha = config.SlotDropTargetAlpha,
                SelectedSlotBorderRed = config.SelectedSlotBorderRed,
                SelectedSlotBorderGreen = config.SelectedSlotBorderGreen,
                SelectedSlotBorderBlue = config.SelectedSlotBorderBlue,
                SelectedSlotBorderAlpha = config.SelectedSlotBorderAlpha,
                TooltipBgRed = config.TooltipBgRed,
                TooltipBgGreen = config.TooltipBgGreen,
                TooltipBgBlue = config.TooltipBgBlue,
                TooltipBgAlpha = config.TooltipBgAlpha,
                TooltipTextRed = config.TooltipTextRed,
                TooltipTextGreen = config.TooltipTextGreen,
                TooltipTextBlue = config.TooltipTextBlue,
                TooltipTextAlpha = config.TooltipTextAlpha,
                TooltipPadding = config.TooltipPadding,
                ContextMenu = Clone(config.ContextMenu),
                PanelUi = Clone(config.PanelUi),
                CooldownLabelStyle = Clone(config.CooldownLabelStyle),
                ChargeLabelStyle = Clone(config.ChargeLabelStyle),
                QuantityLabelStyle = Clone(config.QuantityLabelStyle),
                MacroGearLabelStyle = Clone(config.MacroGearLabelStyle),
            };

        public void ApplyTo(Configuration config)
        {
            config.SlotSize = SlotSize;
            config.SlotPadding = SlotPadding;
            config.WindowPadding = WindowPadding;
            config.WindowBgRed = WindowBgRed;
            config.WindowBgGreen = WindowBgGreen;
            config.WindowBgBlue = WindowBgBlue;
            config.WindowBgAlpha = WindowBgAlpha;
            config.EditModeWindowBgRed = EditModeWindowBgRed;
            config.EditModeWindowBgGreen = EditModeWindowBgGreen;
            config.EditModeWindowBgBlue = EditModeWindowBgBlue;
            config.EditModeWindowBgAlpha = EditModeWindowBgAlpha;
            config.WindowBorderRed = WindowBorderRed;
            config.WindowBorderGreen = WindowBorderGreen;
            config.WindowBorderBlue = WindowBorderBlue;
            config.WindowBorderAlpha = WindowBorderAlpha;
            config.WindowBorderThickness = WindowBorderThickness;
            config.EditModeWindowBorderRed = EditModeWindowBorderRed;
            config.EditModeWindowBorderGreen = EditModeWindowBorderGreen;
            config.EditModeWindowBorderBlue = EditModeWindowBorderBlue;
            config.EditModeWindowBorderAlpha = EditModeWindowBorderAlpha;
            config.EditModeWindowBorderThickness = EditModeWindowBorderThickness;
            config.SlotBgRed = SlotBgRed;
            config.SlotBgGreen = SlotBgGreen;
            config.SlotBgBlue = SlotBgBlue;
            config.SlotBgAlpha = SlotBgAlpha;
            config.SlotDropTargetRed = SlotDropTargetRed;
            config.SlotDropTargetGreen = SlotDropTargetGreen;
            config.SlotDropTargetBlue = SlotDropTargetBlue;
            config.SlotDropTargetAlpha = SlotDropTargetAlpha;
            config.SelectedSlotBorderRed = SelectedSlotBorderRed;
            config.SelectedSlotBorderGreen = SelectedSlotBorderGreen;
            config.SelectedSlotBorderBlue = SelectedSlotBorderBlue;
            config.SelectedSlotBorderAlpha = SelectedSlotBorderAlpha;
            config.TooltipBgRed = TooltipBgRed;
            config.TooltipBgGreen = TooltipBgGreen;
            config.TooltipBgBlue = TooltipBgBlue;
            config.TooltipBgAlpha = TooltipBgAlpha;
            config.TooltipTextRed = TooltipTextRed;
            config.TooltipTextGreen = TooltipTextGreen;
            config.TooltipTextBlue = TooltipTextBlue;
            config.TooltipTextAlpha = TooltipTextAlpha;
            config.TooltipPadding = TooltipPadding;
            config.ContextMenu = Clone(ContextMenu);
            config.PanelUi = Clone(PanelUi);
            config.CooldownLabelStyle = Clone(CooldownLabelStyle);
            config.ChargeLabelStyle = Clone(ChargeLabelStyle);
            config.QuantityLabelStyle = Clone(QuantityLabelStyle);
            config.MacroGearLabelStyle = Clone(MacroGearLabelStyle);
        }

        private static T Clone<T>(T value) =>
            JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!;
    }
}
