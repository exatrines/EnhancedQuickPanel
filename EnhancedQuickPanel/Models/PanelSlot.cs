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

    public bool BadgeUseCustomScale { get; set; }

    public float BadgeScale { get; set; } = 1f;

    public SlotCornerLook BadgeTopLeftLook { get; set; } = new();

    public SlotCornerLook BadgeTopRightLook { get; set; } = new();

    public SlotCornerLook BadgeBottomLeftLook { get; set; } = new();

    public SlotCornerLook BadgeBottomRightLook { get; set; } = new();

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
        BadgeTopLeftLook ??= new();
        BadgeTopRightLook ??= new();
        BadgeBottomLeftLook ??= new();
        BadgeBottomRightLook ??= new();
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
        (BadgeUseCustomScale, other.BadgeUseCustomScale) = (other.BadgeUseCustomScale, BadgeUseCustomScale);
        (BadgeScale, other.BadgeScale) = (other.BadgeScale, BadgeScale);
        (BadgeTopLeftLook, other.BadgeTopLeftLook) = (other.BadgeTopLeftLook, BadgeTopLeftLook);
        (BadgeTopRightLook, other.BadgeTopRightLook) = (other.BadgeTopRightLook, BadgeTopRightLook);
        (BadgeBottomLeftLook, other.BadgeBottomLeftLook) = (other.BadgeBottomLeftLook, BadgeBottomLeftLook);
        (BadgeBottomRightLook, other.BadgeBottomRightLook) = (other.BadgeBottomRightLook, BadgeBottomRightLook);
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
        BadgeUseCustomScale = false;
        BadgeScale = 1f;
        BadgeTopLeftLook = new();
        BadgeTopRightLook = new();
        BadgeBottomLeftLook = new();
        BadgeBottomRightLook = new();
    }

    public SlotCornerLook GetCornerLook(SlotCorner corner) =>
        corner switch
        {
            SlotCorner.TopLeft => BadgeTopLeftLook ??= new(),
            SlotCorner.TopRight => BadgeTopRightLook ??= new(),
            SlotCorner.BottomLeft => BadgeBottomLeftLook ??= new(),
            _ => BadgeBottomRightLook ??= new(),
        };

    public void SetCornerColor(SlotCorner corner, Vector4 color)
    {
        PromoteSlotColorToCorners();
        GetCornerLook(corner).SetColor(color);
    }

    public void ClearCornerColor(SlotCorner corner)
    {
        PromoteSlotColorToCorners();
        GetCornerLook(corner).ClearColor();
    }

    public void SetCornerScale(SlotCorner corner, float scale)
    {
        PromoteSlotScaleToCorners();
        GetCornerLook(corner).SetScale(scale);
    }

    public void ClearCornerScale(SlotCorner corner)
    {
        PromoteSlotScaleToCorners();
        GetCornerLook(corner).ClearScale();
    }

    private void PromoteSlotColorToCorners()
    {
        if (!BadgeUseCustomColor)
            return;

        foreach (var corner in Enum.GetValues<SlotCorner>())
        {
            if (SkipWrenchColorPromote(corner))
                continue;

            var look = GetCornerLook(corner);
            if (!look.UseCustomColor)
                look.SetColor(BadgeColor);
        }

        BadgeUseCustomColor = false;
    }

    private void PromoteSlotScaleToCorners()
    {
        if (!BadgeUseCustomScale)
            return;

        foreach (var corner in Enum.GetValues<SlotCorner>())
        {
            var look = GetCornerLook(corner);
            if (!look.UseCustomScale)
                look.SetScale(BadgeScale);
        }

        BadgeUseCustomScale = false;
    }

    private bool SkipWrenchColorPromote(SlotCorner corner) =>
        corner == SlotCorner.BottomRight
        && Kind == PanelSlotKind.Plugin
        && BadgeBottomRight == 0;

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

public sealed class SlotCornerLook
{
    public const float MinScale = 0.5f;

    public const float MaxScale = 2f;

    public bool UseCustomColor { get; set; }

    public float ColorRed { get; set; } = 1f;

    public float ColorGreen { get; set; } = 1f;

    public float ColorBlue { get; set; } = 1f;

    public float ColorAlpha { get; set; } = 1f;

    public Vector4 Color => new(ColorRed, ColorGreen, ColorBlue, ColorAlpha);

    public bool UseCustomScale { get; set; }

    public float Scale { get; set; } = 1f;

    public void SetColor(Vector4 color)
    {
        UseCustomColor = true;
        ColorRed = color.X;
        ColorGreen = color.Y;
        ColorBlue = color.Z;
        ColorAlpha = color.W;
    }

    public void ClearColor()
    {
        UseCustomColor = false;
        ColorRed = 1f;
        ColorGreen = 1f;
        ColorBlue = 1f;
        ColorAlpha = 1f;
    }

    public void SetScale(float scale)
    {
        UseCustomScale = true;
        Scale = Math.Clamp(scale, MinScale, MaxScale);
    }

    public void ClearScale()
    {
        UseCustomScale = false;
        Scale = 1f;
    }
}

public sealed class PanelPage
{
    public string Name { get; set; } = string.Empty;

    public List<PanelSlot> Slots { get; set; } = [];

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Name) ? T("common.noName") : Name.Trim();
}
