using EnhancedQuickPanel.Services.MacroIcon;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;

namespace EnhancedQuickPanel.Services;

/// <summary>Resolves the icon used by a macro from its lines or the game's macro-icon API.</summary>
internal static unsafe class MacroIconResolver
{
    private const int MaxMacroLines = 15;

    private static bool IsValidMiconRowId(uint id) =>
        id is > 0 and < 0xF000_0000;

    internal static bool TryResolveFromMacro(byte macroSet, byte macroIndex, out uint iconId)
    {
        iconId = 0;
        var macroModule = RaptureMacroModule.Instance();
        if (macroModule == null)
            return false;

        return TryResolveFromMacro(macroModule->GetMacro(macroSet, macroIndex), out iconId);
    }

    internal static bool TryResolveFromMacro(RaptureMacroModule.Macro* macro, out uint iconId)
    {
        iconId = 0;
        if (macro == null)
            return false;

        if (TryResolveFromMacroLines(macro, out iconId))
            return true;

        return TryResolveFromMacroIconCommand(macro, out iconId);
    }

    private static bool TryResolveFromMacroIconCommand(RaptureMacroModule.Macro* macro, out uint iconId)
    {
        iconId = 0;
        var shellModule = RaptureShellModule.Instance();
        if (shellModule == null)
            return false;

        var result = stackalloc MacroIconTextCommand[1];
        if (!shellModule->TryGetMacroIconCommand(macro, result))
            return false;

        if (result->TextCommandId != MacroIconTextCommand.MiconTextCommandId || !IsValidMiconRowId(result->Id))
            return false;

        if (TryParseMiconFromMacro(macro, out var parsed)
            && MacroIconLookup.TryResolve(parsed.Category, parsed.Name, out var entry)
            && entry.IconId != 0)
        {
            iconId = entry.IconId;
            return true;
        }

        return MacroIconLookup.TryResolveByTextCommandCategory(result->Category, result->Id, out iconId);
    }

    private static bool TryResolveFromMacroLines(RaptureMacroModule.Macro* macro, out uint iconId)
    {
        iconId = 0;

        for (var lineIndex = 0; lineIndex < MaxMacroLines; lineIndex++)
        {
            if (macro->Lines[lineIndex].IsEmpty)
                continue;

            foreach (var candidate in MacroContentReader.DecodeLineCandidates(macro->Lines[lineIndex]))
            {
                var line = ExtractMiconCommand(candidate);
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (MacroIconLookup.TryResolveCommand(line, out var entry) && entry.IconId != 0)
                {
                    iconId = entry.IconId;
                    return true;
                }

                if (MacroIconLookup.TryResolveIconId(line, out iconId))
                    return true;
            }
        }

        return false;
    }

    internal static bool TryParseMiconFromMacro(
        RaptureMacroModule.Macro* macro,
        out MacroIconCommandParseResult parsed)
    {
        parsed = default;
        if (macro == null)
            return false;

        for (var lineIndex = 0; lineIndex < MaxMacroLines; lineIndex++)
        {
            if (macro->Lines[lineIndex].IsEmpty)
                continue;

            foreach (var candidate in MacroContentReader.DecodeLineCandidates(macro->Lines[lineIndex]))
            {
                var line = ExtractMiconCommand(candidate);
                if (MacroIconCommandParser.TryParse(line, out parsed))
                    return true;
            }
        }

        return false;
    }

    private static string ExtractMiconCommand(string line)
    {
        var miconIndex = line.IndexOf("/micon", StringComparison.OrdinalIgnoreCase);
        if (miconIndex < 0)
        {
            var macroIconIndex = line.IndexOf("/macroicon", StringComparison.OrdinalIgnoreCase);
            if (macroIconIndex < 0)
                return line;

            return line[macroIconIndex..].Trim();
        }

        return line[miconIndex..].Trim();
    }
}

