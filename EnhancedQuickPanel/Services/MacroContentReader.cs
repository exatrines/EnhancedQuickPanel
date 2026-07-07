using EnhancedQuickPanel.Models;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;

namespace EnhancedQuickPanel.Services;

/// <summary>Reads a macro's lines and metadata for the editor preview and icon resolution.</summary>
internal static unsafe class MacroContentReader
{
    private const int MaxMacroLines = 15;

    /// <summary>Result of reading a macro: availability, status message, display text, and body.</summary>
    internal sealed class MacroContent
    {
        public bool IsAvailable { get; init; }

        public string StatusMessage { get; init; } = string.Empty;

        public string DisplayText { get; init; } = string.Empty;

        public string BodyText { get; init; } = string.Empty;
    }

    public static MacroContent Read(PanelSlot slot) =>
        Read(slot.MacroSet, slot.MacroIndex, slot);

    public static MacroContent Read(byte macroSet, byte macroIndex, PanelSlot? slot = null)
    {
        try
        {
            return ReadCore(macroSet, macroIndex, slot);
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

    private static MacroContent ReadCore(byte macroSet, byte macroIndex, PanelSlot? slot)
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

        var reportedLineCount = (int)macroModule->GetLineCount(macro);
        var lines = new List<string>();
        for (var lineIndex = 0; lineIndex < MaxMacroLines; lineIndex++)
        {
            if (macro->Lines[lineIndex].IsEmpty)
                continue;

            var text = GetPrimaryLineText(macro->Lines[lineIndex]);
            lines.Add(string.IsNullOrEmpty(text) ? T("macroContent.lineDecodeFailed", lineIndex + 1) : text);
        }

        var miconFromLines = MacroIconResolver.TryResolveFromMacro(macro, out var parsedIconId);
        var miconDebug = BuildMiconDebug(macro);
        var quickPanelCommandId = MacroSlotResolver.EncodeQuickPanelMacroCommandId(macroSet, macroIndex);
        var display = new System.Text.StringBuilder();
        display.AppendLine(T("macroContent.debugReference", macroSet == 0 ? T("macroContent.referencePersonal") : T("macroContent.referenceShared"), macroIndex + 1, macroIndex));
        display.AppendLine($"Hotbar CommandId: {MacroSlotResolver.EncodeHotbarCommandId(macroSet, macroIndex)}");
        display.AppendLine($"QuickPanel CommandId: {quickPanelCommandId}");
        if (slot?.CommandId > 0 && slot.CommandId != quickPanelCommandId)
            display.AppendLine($"Slot CommandId: {slot.CommandId}");
        display.AppendLine($"IsNotEmpty: {macro->IsNotEmpty()}");
        display.AppendLine($"GetLineCount: {reportedLineCount}");
        display.AppendLine(T("macroContent.debugLineCount", lines.Count));
        display.AppendLine(T("macroContent.debugName", GetMacroName(macro)));
        display.AppendLine(T("macroContent.debugSavedIconId", macro->IconId));
        display.AppendLine($"MacroIconRowId: {macro->MacroIconRowId}");
        if (!string.IsNullOrEmpty(miconDebug))
            display.AppendLine(miconDebug);

        display.AppendLine(
            T("macroContent.debugParsedIcon", miconFromLines ? parsedIconId.ToString() : T("macroContent.debugParseUnresolved")));

        display.AppendLine();
        if (lines.Count == 0)
            display.AppendLine(T("slot.editor.macroNoLines"));
        else
        {
            for (var i = 0; i < lines.Count; i++)
                display.AppendLine($"{i + 1,2}: {lines[i]}");
        }

        var bodyText = lines.Count == 0
            ? string.Empty
            : string.Join('\n', lines);

        return new MacroContent
        {
            IsAvailable = true,
            StatusMessage = T("macroContent.readSuccess"),
            DisplayText = display.ToString().TrimEnd(),
            BodyText = bodyText,
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

    private static string GetMacroName(RaptureMacroModule.Macro* macro)
    {
        if (macro == null || macro->Name.IsEmpty)
            return T("common.noName");

        try
        {
            var name = macro->Name.GetText();
            if (!string.IsNullOrWhiteSpace(name))
                return name.Trim();
        }
        catch
        {
            // ignored
        }

        try
        {
            var name = macro->Name.ToString();
            return string.IsNullOrWhiteSpace(name) ? T("common.noName") : name.Trim();
        }
        catch
        {
            return T("common.noName");
        }
    }

    private static string BuildMiconDebug(RaptureMacroModule.Macro* macro)
    {
        var shellModule = RaptureShellModule.Instance();
        if (shellModule == null)
            return string.Empty;

        try
        {
            var result = stackalloc MacroIcon.MacroIconTextCommand[1];
            if (!shellModule->TryGetMacroIconCommand(macro, result))
                return T("macroContent.miconNotFound");

            var idValid = result->Id is > 0 and < 0xF000_0000;
            return
                $"/micon API: TextCommandId={result->TextCommandId}, Category={result->Category}, Id={result->Id}, IdValid={idValid}";
        }
        catch
        {
            return T("macroContent.miconReadFailed");
        }
    }
}
