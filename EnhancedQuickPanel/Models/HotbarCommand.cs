using System.Runtime.CompilerServices;

namespace EnhancedQuickPanel.Models;

/// <summary>Command id 0 is empty except for index-based hotbar types such as GearSet.</summary>
internal static class HotbarCommand
{
    public static bool IsConfiguredAction(byte commandType, uint commandId) =>
        commandId != 0 || AllowsZeroCommandType(commandType);

    public static bool IsAssigned<TEnum>(TEnum commandType, uint commandId)
        where TEnum : struct, Enum =>
        IsAssigned(ToByte(commandType), commandId);

    public static bool IsAssigned(byte commandType, uint commandId) =>
        commandType != 0 && (commandId != 0 || AllowsZeroCommandType(commandType));

    public static bool AllowsZeroCommandType<TEnum>(TEnum commandType)
        where TEnum : struct, Enum =>
        AllowsZeroCommandType(ToByte(commandType));

    public static bool AllowsZeroCommandType(byte commandType) =>
        commandType is TypeMacro or TypeMarker or TypeGearSet or TypeFieldMarker;

    private static byte ToByte<TEnum>(TEnum commandType)
        where TEnum : struct, Enum =>
        Unsafe.BitCast<TEnum, byte>(commandType);

    private const byte TypeMacro = 5;
    private const byte TypeMarker = 6;
    private const byte TypeGearSet = 12;
    private const byte TypeFieldMarker = 15;
}
