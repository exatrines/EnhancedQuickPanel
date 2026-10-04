namespace EnhancedQuickPanel.Models;

public enum PanelSlotKind
{
    Empty = 0,
    Action = 1,
    Macro = 2,
    TextCommand = 3,
    Plugin = 4,
    Dalamud = 5,
}

public enum DalamudShortcutKind
{
    None = 0,
    Plugins = 1,
    Settings = 2,
    Data = 3,
    Log = 4,
}

public enum PluginShortcutAction
{
    None = 0,
    MainUi = 1,
    ConfigUi = 2,
    TextCommand = 3,
    ToggleEnabled = 5,
}

public enum SlotCorner : byte
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

public sealed class PanelSlot
{
    public PanelSlotKind Kind { get; set; } = PanelSlotKind.Empty;

    /// <summary>RaptureHotbarModule.HotbarSlotType for Action kind.</summary>
    public byte CommandType { get; set; }

    public uint CommandId { get; set; }

    /// <summary>0 = individual, 1 = shared.</summary>
    public byte MacroSet { get; set; }

    /// <summary>0-99 macro index; macro #N uses index N.</summary>
    public byte MacroIndex { get; set; }

    public string Label { get; set; } = string.Empty;

    public uint IconId { get; set; }

    public string TextBody { get; set; } = string.Empty;

    public DalamudShortcutKind DalamudShortcut { get; set; } = DalamudShortcutKind.Plugins;

    public string PluginInternalName { get; set; } = string.Empty;
    public string PluginWorkingPluginId { get; set; } = string.Empty;
    public PluginShortcutAction LeftClickAction { get; set; } = PluginShortcutAction.MainUi;
    public PluginShortcutAction RightClickAction { get; set; } = PluginShortcutAction.ConfigUi;
    public PluginShortcutAction MiddleClickAction { get; set; } = PluginShortcutAction.ToggleEnabled;
    public string LeftClickCommand { get; set; } = string.Empty;
    public string RightClickCommand { get; set; } = string.Empty;
    public string MiddleClickCommand { get; set; } = string.Empty;

    public ushort BadgeTopLeft { get; set; }

    public ushort BadgeTopRight { get; set; }

    public ushort BadgeBottomLeft { get; set; }

    public ushort BadgeBottomRight { get; set; }

    public bool BadgeUseCustomColor { get; set; }

    public float BadgeColorRed { get; set; } = 1f;

    public float BadgeColorGreen { get; set; } = 1f;

    public float BadgeColorBlue { get; set; } = 1f;

    public float BadgeColorAlpha { get; set; } = 1f;

    public Vector4 BadgeColor => new(BadgeColorRed, BadgeColorGreen, BadgeColorBlue, BadgeColorAlpha);

    public bool ShowOutline { get; set; }

    public bool OutlineUseCustomColor { get; set; }

    public float OutlineColorRed { get; set; } = 1f;

    public float OutlineColorGreen { get; set; } = 1f;

    public float OutlineColorBlue { get; set; } = 1f;

    public float OutlineColorAlpha { get; set; } = 1f;

    public Vector4 OutlineColor => new(OutlineColorRed, OutlineColorGreen, OutlineColorBlue, OutlineColorAlpha);

    public bool OutlineUseCustomThickness { get; set; }

    public float OutlineThickness { get; set; } = 2f;

    public bool IsConfigured =>
        Kind switch
        {
            PanelSlotKind.Action => HotbarCommand.IsConfiguredAction(CommandType, CommandId),
            PanelSlotKind.Macro => MacroIndex < 100,
            PanelSlotKind.TextCommand => !string.IsNullOrWhiteSpace(TextBody),
            PanelSlotKind.Dalamud => DalamudShortcut != DalamudShortcutKind.None,
            PanelSlotKind.Plugin => !string.IsNullOrWhiteSpace(PluginInternalName),
            _ => false,
        };

    public void Sanitize()
    {
        SanitizePluginShortcut();
        SanitizeDalamudShortcut();
    }

    public void SanitizePluginShortcut()
    {
        LeftClickAction = SanitizeAction(LeftClickAction);
        RightClickAction = SanitizeAction(RightClickAction);
        MiddleClickAction = SanitizeAction(MiddleClickAction);
    }

    public void SanitizeDalamudShortcut()
    {
        if (!Enum.IsDefined(DalamudShortcut))
            DalamudShortcut = DalamudShortcutKind.None;
        if (Kind != PanelSlotKind.Dalamud)
            return;
        if (DalamudShortcut == DalamudShortcutKind.None)
            DalamudShortcut = ParseLegacyDalamudCommand(TextBody);
        TextBody = string.Empty;
    }

    public void Clear()
    {
        Kind = PanelSlotKind.Empty;
        Label = string.Empty;
        IconId = 0;
        TextBody = string.Empty;
        CommandType = 0;
        CommandId = 0;
        MacroSet = 0;
        MacroIndex = 0;
        ResetPluginShortcut();
        ResetDalamudShortcut();
        ClearCornerBadges();
        ClearOutline();
    }

    public void SwapContentsWith(PanelSlot other)
    {
        (Kind, other.Kind) = (other.Kind, Kind);
        (CommandType, other.CommandType) = (other.CommandType, CommandType);
        (CommandId, other.CommandId) = (other.CommandId, CommandId);
        (MacroSet, other.MacroSet) = (other.MacroSet, MacroSet);
        (MacroIndex, other.MacroIndex) = (other.MacroIndex, MacroIndex);
        (IconId, other.IconId) = (other.IconId, IconId);
        (Label, other.Label) = (other.Label, Label);
        (TextBody, other.TextBody) = (other.TextBody, TextBody);
        (DalamudShortcut, other.DalamudShortcut) = (other.DalamudShortcut, DalamudShortcut);
        (BadgeTopLeft, other.BadgeTopLeft) = (other.BadgeTopLeft, BadgeTopLeft);
        (BadgeTopRight, other.BadgeTopRight) = (other.BadgeTopRight, BadgeTopRight);
        (BadgeBottomLeft, other.BadgeBottomLeft) = (other.BadgeBottomLeft, BadgeBottomLeft);
        (BadgeBottomRight, other.BadgeBottomRight) = (other.BadgeBottomRight, BadgeBottomRight);
        (BadgeUseCustomColor, other.BadgeUseCustomColor) = (other.BadgeUseCustomColor, BadgeUseCustomColor);
        (BadgeColorRed, other.BadgeColorRed) = (other.BadgeColorRed, BadgeColorRed);
        (BadgeColorGreen, other.BadgeColorGreen) = (other.BadgeColorGreen, BadgeColorGreen);
        (BadgeColorBlue, other.BadgeColorBlue) = (other.BadgeColorBlue, BadgeColorBlue);
        (BadgeColorAlpha, other.BadgeColorAlpha) = (other.BadgeColorAlpha, BadgeColorAlpha);
        (ShowOutline, other.ShowOutline) = (other.ShowOutline, ShowOutline);
        (OutlineUseCustomColor, other.OutlineUseCustomColor) = (other.OutlineUseCustomColor, OutlineUseCustomColor);
        (OutlineColorRed, other.OutlineColorRed) = (other.OutlineColorRed, OutlineColorRed);
        (OutlineColorGreen, other.OutlineColorGreen) = (other.OutlineColorGreen, OutlineColorGreen);
        (OutlineColorBlue, other.OutlineColorBlue) = (other.OutlineColorBlue, OutlineColorBlue);
        (OutlineColorAlpha, other.OutlineColorAlpha) = (other.OutlineColorAlpha, OutlineColorAlpha);
        (OutlineUseCustomThickness, other.OutlineUseCustomThickness) = (other.OutlineUseCustomThickness, OutlineUseCustomThickness);
        (OutlineThickness, other.OutlineThickness) = (other.OutlineThickness, OutlineThickness);
        SwapPluginShortcutWith(other);
    }

    public ushort GetCornerBadge(SlotCorner corner) =>
        corner switch
        {
            SlotCorner.TopLeft => BadgeTopLeft,
            SlotCorner.TopRight => BadgeTopRight,
            SlotCorner.BottomLeft => BadgeBottomLeft,
            SlotCorner.BottomRight => BadgeBottomRight,
            _ => 0,
        };

    public void SetCornerBadge(SlotCorner corner, ushort value)
    {
        switch (corner)
        {
            case SlotCorner.TopLeft:
                BadgeTopLeft = value;
                break;
            case SlotCorner.TopRight:
                BadgeTopRight = value;
                break;
            case SlotCorner.BottomLeft:
                BadgeBottomLeft = value;
                break;
            case SlotCorner.BottomRight:
                BadgeBottomRight = value;
                break;
        }
    }

    public void SetBadgeColor(Vector4 color)
    {
        BadgeUseCustomColor = true;
        BadgeColorRed = color.X;
        BadgeColorGreen = color.Y;
        BadgeColorBlue = color.Z;
        BadgeColorAlpha = color.W;
    }

    public void ClearCornerBadges()
    {
        BadgeTopLeft = 0;
        BadgeTopRight = 0;
        BadgeBottomLeft = 0;
        BadgeBottomRight = 0;
        BadgeUseCustomColor = false;
        BadgeColorRed = 1f;
        BadgeColorGreen = 1f;
        BadgeColorBlue = 1f;
        BadgeColorAlpha = 1f;
    }

    public void SetOutlineColor(Vector4 color)
    {
        OutlineUseCustomColor = true;
        OutlineColorRed = color.X;
        OutlineColorGreen = color.Y;
        OutlineColorBlue = color.Z;
        OutlineColorAlpha = color.W;
    }

    public void SetOutlineThickness(float thickness)
    {
        OutlineUseCustomThickness = true;
        OutlineThickness = Math.Clamp(thickness, 1f, 8f);
    }

    public void ClearOutline()
    {
        ShowOutline = false;
        OutlineUseCustomColor = false;
        OutlineColorRed = 1f;
        OutlineColorGreen = 1f;
        OutlineColorBlue = 1f;
        OutlineColorAlpha = 1f;
        OutlineUseCustomThickness = false;
        OutlineThickness = 2f;
    }

    public void ResetDalamudShortcut() =>
        DalamudShortcut = DalamudShortcutKind.Plugins;

    public void ResetPluginShortcut()
    {
        PluginInternalName = string.Empty;
        PluginWorkingPluginId = string.Empty;
        LeftClickAction = PluginShortcutAction.MainUi;
        RightClickAction = PluginShortcutAction.ConfigUi;
        MiddleClickAction = PluginShortcutAction.ToggleEnabled;
        LeftClickCommand = string.Empty;
        RightClickCommand = string.Empty;
        MiddleClickCommand = string.Empty;
    }

    public void SwapPluginShortcutWith(PanelSlot other)
    {
        (PluginInternalName, other.PluginInternalName) = (other.PluginInternalName, PluginInternalName);
        (PluginWorkingPluginId, other.PluginWorkingPluginId) = (other.PluginWorkingPluginId, PluginWorkingPluginId);
        (LeftClickAction, other.LeftClickAction) = (other.LeftClickAction, LeftClickAction);
        (RightClickAction, other.RightClickAction) = (other.RightClickAction, RightClickAction);
        (MiddleClickAction, other.MiddleClickAction) = (other.MiddleClickAction, MiddleClickAction);
        (LeftClickCommand, other.LeftClickCommand) = (other.LeftClickCommand, LeftClickCommand);
        (RightClickCommand, other.RightClickCommand) = (other.RightClickCommand, RightClickCommand);
        (MiddleClickCommand, other.MiddleClickCommand) = (other.MiddleClickCommand, MiddleClickCommand);
    }

    private static PluginShortcutAction SanitizeAction(PluginShortcutAction action) =>
        Enum.IsDefined(action) ? action : PluginShortcutAction.None;

    private static DalamudShortcutKind ParseLegacyDalamudCommand(string textBody) =>
        textBody.Trim().ToLowerInvariant() switch
        {
            "/xlplugins" => DalamudShortcutKind.Plugins,
            "/xlsettings" => DalamudShortcutKind.Settings,
            "/xldata" => DalamudShortcutKind.Data,
            "/xllog" => DalamudShortcutKind.Log,
            _ => DalamudShortcutKind.Plugins,
        };
}

public sealed class PanelPage
{
    public string Name { get; set; } = string.Empty;

    public List<PanelSlot> Slots { get; set; } = [];

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Name) ? T("common.noName") : Name.Trim();
}
