using EnhancedQuickPanel.Models;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace EnhancedQuickPanel.Services;

// Reads macro lines for the slot editor preview and icon resolution.
internal static unsafe class MacroContentReader
{
    private const int MaxMacroLines = 15;

    internal sealed class MacroContent
    {
        public bool IsAvailable { get; init; }

        public string StatusMessage { get; init; } = string.Empty;

        public string BodyText { get; init; } = string.Empty;
    }

    public static MacroContent Read(PanelSlot slot) =>
        Read(slot.MacroSet, slot.MacroIndex);

    public static MacroContent Read(byte macroSet, byte macroIndex)
    {
        try
        {
            return ReadCore(macroSet, macroIndex);
        }
        catch (Exception ex)
        {
            return new MacroContent
            {
                IsAvailable = false,
                StatusMessage = T("macroContent.readFailed", ex.Message),
            };
        }
    }

    private static MacroContent ReadCore(byte macroSet, byte macroIndex)
    {
        if (!GameModuleGuard.TryGetMacroModule(out var macroModule))
        {
            return new MacroContent
            {
                IsAvailable = false,
                StatusMessage = T("macroContent.moduleUnavailable"),
            };
        }

        RaptureMacroModule.Macro* macro;
        try
        {
            macro = macroModule->GetMacro(macroSet, macroIndex);
        }
        catch (Exception ex)
        {
            return new MacroContent
            {
                IsAvailable = false,
                StatusMessage = T("macroContent.pointerFailed", ex.Message),
            };
        }

        if (macro == null)
        {
            return new MacroContent
            {
                IsAvailable = false,
                StatusMessage = T("macroContent.notFound"),
            };
        }

        var lines = new List<string>();
        for (var lineIndex = 0; lineIndex < MaxMacroLines; lineIndex++)
        {
            if (macro->Lines[lineIndex].IsEmpty)
                continue;

            var text = GetPrimaryLineText(macro->Lines[lineIndex]);
            lines.Add(string.IsNullOrEmpty(text) ? T("macroContent.lineDecodeFailed", lineIndex + 1) : text);
        }

        return new MacroContent
        {
            IsAvailable = true,
            BodyText = lines.Count == 0 ? string.Empty : string.Join('\n', lines),
        };
    }

    internal static string GetPrimaryLineText(Utf8String lineUtf8)
    {
        foreach (var candidate in DecodeLineCandidates(lineUtf8))
        {
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;
        }

        return string.Empty;
    }

    internal static IEnumerable<string> DecodeLineCandidates(Utf8String lineUtf8)
    {
        if (lineUtf8.IsEmpty)
            return [];

        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void AddCandidate(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            var trimmed = text.Trim();
            if (trimmed.Length == 0 || !seen.Add(trimmed))
                return;

            candidates.Add(trimmed);
        }

        try
        {
            AddCandidate(lineUtf8.GetText());
        }
        catch
        {
            // ignored
        }

        try
        {
            AddCandidate(lineUtf8.ToString());
        }
        catch
        {
            // ignored
        }

        return candidates;
    }
}
