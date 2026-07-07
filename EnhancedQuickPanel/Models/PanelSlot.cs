namespace EnhancedQuickPanel.Models;

/// <summary>The kind of content a slot holds (empty, action, macro, or text command).</summary>
public enum PanelSlotKind
{
    Empty = 0,
    Action = 1,
    Macro = 2,
    TextCommand = 3,
}

/// <summary>A single configurable panel slot and its content.</summary>
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

    public bool IsConfigured =>
        Kind switch
        {
            PanelSlotKind.Action => CommandId != 0,
            PanelSlotKind.Macro => MacroIndex < 100,
            PanelSlotKind.TextCommand => !string.IsNullOrWhiteSpace(TextBody),
            _ => false,
        };
}

/// <summary>A named page containing a list of slots.</summary>
public sealed class PanelPage
{
    public string Name { get; set; } = string.Empty;

    public List<PanelSlot> Slots { get; set; } = [];
}

