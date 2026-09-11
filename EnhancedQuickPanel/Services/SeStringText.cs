using System.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Memory;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.System.String;
using InteropGenerator.Runtime;
using Lumina.Text.ReadOnly;

namespace EnhancedQuickPanel.Services;

/// <summary>Decodes SeString text and detects unparsed payloads.</summary>
internal static unsafe class SeStringText
{
    public static string GetText(this SeString seStr)
    {
        var sb = new StringBuilder();
        foreach (var payload in seStr.Payloads)
        {
            if (payload is TextPayload text)
                sb.Append(text.Text);
            else if (payload is AutoTranslatePayload autoTranslate)
                sb.Append(autoTranslate.Text);
            else if (payload.Type == PayloadType.Unknown
                     && payload.Encode().SequenceEqual<byte>([0x02, 0x1d, 0x01, 0x03]))
            {
                sb.Append(' ');
            }
        }

        return sb.ToString();
    }

    public static string GetText(this Utf8String str)
    {
        if (str.StringPtr.Value == null || str.Length <= 0)
            return string.Empty;

        return MemoryHelper.ReadSeStringNullTerminated((nint)str.StringPtr.Value).GetText();
    }

    public static string GetText(this ReadOnlySeString str) =>
        str.ToDalamudString().GetText();

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
            PluginServices.Log.Debug($"[EQP] SeString parse failed: {ex.Message}");
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
            PluginServices.Log.Debug($"[EQP] Utf8String parse failed: {ex.Message}");
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
