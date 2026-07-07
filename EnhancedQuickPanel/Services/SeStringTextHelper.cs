using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.System.String;
using InteropGenerator.Runtime;

namespace EnhancedQuickPanel.Services;

/// <summary>Helpers for decoding SeString text and detecting unparsed payloads.</summary>
internal static unsafe class SeStringTextHelper
{
    public static string ReadPlainText(CStringPointer cString)
    {
        try
        {
            if (cString.Value == null)
                return string.Empty;

            return MemoryHelper.ReadSeStringNullTerminated((nint)cString.Value).GetText().Trim();
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"[EQP] SeString parse failed: {ex.Message}");
            return string.Empty;
        }
    }

    public static string ReadPlainText(Utf8String utf8String)
    {
        try
        {
            if (utf8String.StringPtr.Value == null)
                return string.Empty;

            return MemoryHelper.ReadSeStringNullTerminated((nint)utf8String.StringPtr.Value).GetText().Trim();
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"[EQP] Utf8String parse failed: {ex.Message}");
            return utf8String.ToString().Trim();
        }
    }

    public static bool LooksLikeUnparsedSeString(string text) =>
        !string.IsNullOrWhiteSpace(text)
        && (text.Contains("=H===", StringComparison.Ordinal)
            || text.Contains("=I===", StringComparison.Ordinal)
            || text.Contains("=E===", StringComparison.Ordinal)
            || text.Contains('\uFF1D'));
}
