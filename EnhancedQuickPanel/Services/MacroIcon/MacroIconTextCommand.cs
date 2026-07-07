using System.Runtime.InteropServices;

namespace EnhancedQuickPanel.Services.MacroIcon;

[StructLayout(LayoutKind.Explicit, Size = 0x120)]
/// <summary>Layout matching the game's macro-icon text command result.</summary>
internal struct MacroIconTextCommand
{
    public const ushort MiconTextCommandId = 207;
    public const int DirectIconCategoryA = 270;
    public const int DirectIconCategoryB = 271;

    [FieldOffset(0x00)] public ushort TextCommandId;
    [FieldOffset(0x08)] public uint Id;
    [FieldOffset(0x0C)] public int Category;
}

