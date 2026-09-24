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

    public bool IsConfigured =>
        Kind switch
        {
            PanelSlotKind.Action => CommandId != 0,
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
        SwapPluginShortcutWith(other);
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
