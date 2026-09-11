using EnhancedQuickPanel.Services;
using Lumina.Excel.Sheets;
using GameAction = Lumina.Excel.Sheets.Action;

namespace EnhancedQuickPanel.Services.MacroIcon;

/// <summary>Resolves /micon commands and category/row IDs to icon IDs via game sheets.</summary>
internal static class MacroIconLookup
{
    private static readonly Dictionary<MacroIconCategory, List<MacroIconEntry>> Indexes = [];
    private static bool _initialized;

    internal static bool TryResolveCommand(string command, out MacroIconEntry entry)
    {
        EnsureInitialized();
        entry = default;

        if (!MacroIconCommandParser.TryParse(command, out var parsed))
            return false;

        return TryResolve(parsed.Category, parsed.Name, out entry);
    }

    internal static bool TryResolveIconId(string command, out uint iconId)
    {
        iconId = 0;
        if (!MacroIconCommandParser.TryParseIconId(command, out iconId))
            return false;

        return iconId > 0;
    }

    internal static bool TryResolveByTextCommandCategory(int category, uint id, out uint iconId)
    {
        iconId = 0;
        if (id is 0 or >= 0xF000_0000)
            return false;

        if (category is MacroIconTextCommand.DirectIconCategoryA or MacroIconTextCommand.DirectIconCategoryB)
        {
            iconId = id;
            return IsLoadableIcon(iconId);
        }

        if (TryResolveCategoryParam(category, out var macroCategory)
            && TryResolveIconByRow(macroCategory, id, out iconId))
        {
            return true;
        }

        return TryResolveIconByRow(MacroIconCategory.Action, id, out iconId);
    }

    internal static bool TryResolveIconByRow(MacroIconCategory category, uint rowId, out uint iconId)
    {
        iconId = category switch
        {
            MacroIconCategory.Action => GetSheetIcon<GameAction>(rowId, row => row.Icon),
            MacroIconCategory.MacroIcon => TryResolveMacroIcon(rowId, out iconId) ? iconId : 0,
            MacroIconCategory.Emote => GetSheetIcon<Emote>(rowId, row => row.Icon),
            MacroIconCategory.Buddy => GetSheetIcon<BuddyAction>(rowId, row => (uint)row.Icon),
            MacroIconCategory.Pet => GetSheetIcon<PetAction>(rowId, row => (uint)row.Icon),
            MacroIconCategory.Minion => GetSheetIcon<Companion>(rowId, row => row.Icon),
            MacroIconCategory.Mount => GetSheetIcon<Mount>(rowId, row => row.Icon),
            MacroIconCategory.Item => GetSheetIcon<Item>(rowId, row => row.Icon),
            MacroIconCategory.Marking => GetSheetIcon<Marker>(rowId, row => (uint)row.Icon),
            MacroIconCategory.FieldMarking => GetSheetIcon<FieldMarker>(rowId, row => row.UiIcon),
            MacroIconCategory.QuickChat => GetSheetIcon<QuickChat>(rowId, row => (uint)row.Icon),
            MacroIconCategory.ClassJob => GetClassJobIcon(rowId),
            _ => 0,
        };

        return IsLoadableIcon(iconId);
    }

    private static bool TryResolveCategoryParam(int category, out MacroIconCategory macroCategory)
    {
        macroCategory = default;
        EnsureInitialized();

        if (_textCommandCategories != null
            && _textCommandCategories.TryGetValue((uint)category, out macroCategory))
        {
            return true;
        }

        var row = PluginServices.Data.GetExcelSheet<TextCommandParam>()?.GetRowOrDefault((uint)category);
        if (row == null)
            return false;

        return MacroIconCommandParser.TryGetCategory(row.Value.Param.GetText(), out macroCategory);
    }

    private static bool TryResolveMacroIcon(uint rowId, out uint iconId)
    {
        if (!MacroIconCatalogIcons.Contains(rowId))
        {
            iconId = 0;
            return false;
        }

        iconId = rowId;
        return IsLoadableIcon(iconId);
    }

    private static uint GetSheetIcon<T>(uint rowId, Func<T, uint> getIcon) where T : struct, Lumina.Excel.IExcelRow<T>
    {
        var row = PluginServices.Data.GetExcelSheet<T>()?.GetRowOrDefault(rowId);
        return row == null ? 0 : getIcon(row.Value);
    }

    private static uint GetClassJobIcon(uint rowId)
    {
        if (PluginServices.Data.GetExcelSheet<ClassJob>()?.GetRowOrDefault(rowId) == null)
            return 0;

        return rowId == 0 ? 62143u : 62100u + rowId;
    }

    private static bool IsLoadableIcon(uint iconId) => iconId is > 0 and <= ResolvedSlotIcon.MaxResolvableIconId;

    private static readonly HashSet<uint> ExcludedBrowsableIconIds = [405, 786];

    private static readonly (string Param, uint RowId)[] MarkingParamAliases =
    [
        ("attack1", 1), ("attack2", 2), ("attack3", 3), ("attack4", 4), ("attack5", 5),
        ("attack6", 15), ("attack7", 16), ("attack8", 17),
        ("bind1", 6), ("bind2", 7), ("bind3", 8),
        ("stop1", 9), ("stop2", 10),
        ("ignore1", 9), ("ignore2", 10),
        ("square", 11), ("circle", 12), ("plus", 13), ("cross", 13), ("triangle", 14),
    ];

    // FieldMarker sheet: rows 1-4 = letter waymarks (A-D), rows 5-8 = numeric waymarks (1-4).
    private static readonly (string Name, uint RowId)[] WaymarkNameAliases =
    [
        ("a", 1), ("b", 2), ("c", 3), ("d", 4),
        ("1", 5), ("2", 6), ("3", 7), ("4", 8),
    ];

    private static Dictionary<uint, MacroIconCategory>? _textCommandCategories;

    internal static bool TryResolve(MacroIconCategory category, string name, out MacroIconEntry entry)
    {
        EnsureInitialized();
        entry = default;

        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (!Indexes.TryGetValue(category, out var entries))
            return false;

        var normalized = Normalize(name);
        var match = entries.FirstOrDefault(e => Normalize(e.Name).Equals(normalized, StringComparison.Ordinal));
        if (string.IsNullOrEmpty(match.Name))
            return false;

        entry = match;
        return true;
    }

    private static void EnsureInitialized()
    {
        if (_initialized)
            return;

        Indexes[MacroIconCategory.Action] = BuildActionIndex();
        Indexes[MacroIconCategory.MacroIcon] = BuildMacroIconIndex();
        Indexes[MacroIconCategory.Emote] = BuildEmoteIndex();
        Indexes[MacroIconCategory.Buddy] = BuildBuddyActionIndex();
        Indexes[MacroIconCategory.Pet] = BuildPetActionIndex();
        Indexes[MacroIconCategory.Minion] = BuildMinionIndex();
        Indexes[MacroIconCategory.Mount] = BuildMountIndex();
        Indexes[MacroIconCategory.Item] = BuildItemIndex();
        Indexes[MacroIconCategory.Marking] = BuildMarkingIndex();
        Indexes[MacroIconCategory.FieldMarking] = BuildFieldMarkingIndex();
        Indexes[MacroIconCategory.ClassJob] = BuildClassJobIndex();
        Indexes[MacroIconCategory.QuickChat] = BuildQuickChatIndex();
        _textCommandCategories = BuildTextCommandCategoryMap();

        _initialized = true;
    }

    private static Dictionary<uint, MacroIconCategory> BuildTextCommandCategoryMap()
    {
        var map = new Dictionary<uint, MacroIconCategory>();
        var sheet = PluginServices.Data.GetExcelSheet<TextCommandParam>();
        if (sheet == null)
            return map;

        foreach (var row in sheet)
        {
            var param = row.Param.GetText().Trim();
            if (string.IsNullOrWhiteSpace(param))
                continue;

            if (MacroIconCommandParser.TryGetCategory(param, out var category))
                map[row.RowId] = category;
        }

        return map;
    }

    private static List<MacroIconEntry> BuildActionIndex()
    {
        var sheet = PluginServices.Data.GetExcelSheet<GameAction>();
        if (sheet == null)
            return [];

        var entries = new List<MacroIconEntry>();
        foreach (var row in sheet)
        {
            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name) || row.Icon == 0 || ExcludedBrowsableIconIds.Contains((uint)row.Icon))
                continue;

            entries.Add(new MacroIconEntry(MacroIconCategory.Action, name, row.Icon, row.RowId));
        }

        return DeduplicateByName(entries);
    }

    private static List<MacroIconEntry> BuildMacroIconIndex() =>
        MacroIconCatalogIcons.BuildEntries().ToList();

    private static List<MacroIconEntry> BuildEmoteIndex()
    {
        var sheet = PluginServices.Data.GetExcelSheet<Emote>();
        if (sheet == null)
            return [];

        var entries = new List<MacroIconEntry>();
        foreach (var row in sheet)
        {
            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name) || row.Icon == 0)
                continue;

            entries.Add(new MacroIconEntry(MacroIconCategory.Emote, name, row.Icon, row.RowId));
        }

        return DeduplicateByName(entries);
    }

    private static List<MacroIconEntry> BuildBuddyActionIndex()
    {
        var sheet = PluginServices.Data.GetExcelSheet<BuddyAction>();
        if (sheet == null)
            return [];

        var entries = new List<MacroIconEntry>();
        foreach (var row in sheet)
        {
            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name) || row.Icon == 0)
                continue;

            entries.Add(new MacroIconEntry(MacroIconCategory.Buddy, name, (uint)row.Icon, row.RowId));
        }

        return DeduplicateByName(entries);
    }

    private static List<MacroIconEntry> BuildPetActionIndex()
    {
        var sheet = PluginServices.Data.GetExcelSheet<PetAction>();
        if (sheet == null)
            return [];

        var entries = new List<MacroIconEntry>();
        foreach (var row in sheet)
        {
            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name) || row.Icon == 0)
                continue;

            entries.Add(new MacroIconEntry(MacroIconCategory.Pet, name, (uint)row.Icon, row.RowId));
        }

        return DeduplicateByName(entries);
    }

    private static List<MacroIconEntry> BuildMinionIndex()
    {
        var sheet = PluginServices.Data.GetExcelSheet<Companion>();
        if (sheet == null)
            return [];

        var entries = new List<MacroIconEntry>();
        foreach (var row in sheet)
        {
            var name = row.Singular.ToString();
            if (string.IsNullOrWhiteSpace(name) || row.Icon == 0)
                continue;

            entries.Add(new MacroIconEntry(MacroIconCategory.Minion, name, row.Icon, row.RowId));
        }

        return DeduplicateByName(entries);
    }

    private static List<MacroIconEntry> BuildMountIndex()
    {
        var sheet = PluginServices.Data.GetExcelSheet<Mount>();
        if (sheet == null)
            return [];

        var entries = new List<MacroIconEntry>();
        foreach (var row in sheet)
        {
            var name = row.Singular.ToString();
            if (string.IsNullOrWhiteSpace(name) || row.Icon == 0)
                continue;

            entries.Add(new MacroIconEntry(MacroIconCategory.Mount, name, row.Icon, row.RowId));
        }

        return DeduplicateByName(entries);
    }

    private static List<MacroIconEntry> BuildItemIndex()
    {
        var sheet = PluginServices.Data.GetExcelSheet<Item>();
        if (sheet == null)
            return [];

        var entries = new List<MacroIconEntry>();
        foreach (var row in sheet)
        {
            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name) || row.Icon == 0)
                continue;

            entries.Add(new MacroIconEntry(MacroIconCategory.Item, name, row.Icon, row.RowId));
        }

        return DeduplicateByName(entries);
    }

    private static List<MacroIconEntry> BuildMarkingIndex()
    {
        var sheet = PluginServices.Data.GetExcelSheet<Marker>();
        if (sheet == null)
            return [];

        var entries = new List<MacroIconEntry>();
        foreach (var row in sheet)
        {
            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name) || row.Icon == 0)
                continue;

            entries.Add(new MacroIconEntry(MacroIconCategory.Marking, name, (uint)row.Icon, row.RowId));
        }

        foreach (var (param, rowId) in MarkingParamAliases)
        {
            var row = sheet.GetRowOrDefault(rowId);
            if (row == null || row.Value.Icon == 0)
                continue;

            entries.Add(new MacroIconEntry(MacroIconCategory.Marking, param, (uint)row.Value.Icon, rowId));
        }

        return DeduplicateByName(entries);
    }

    private static List<MacroIconEntry> BuildFieldMarkingIndex()
    {
        var sheet = PluginServices.Data.GetExcelSheet<FieldMarker>();
        if (sheet == null)
            return [];

        var entries = new List<MacroIconEntry>();
        foreach (var row in sheet)
        {
            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name) || row.UiIcon == 0)
                continue;

            entries.Add(new MacroIconEntry(MacroIconCategory.FieldMarking, name, row.UiIcon, row.RowId));
        }

        for (uint rowId = 1; rowId <= 8; rowId++)
        {
            var row = sheet.GetRowOrDefault(rowId);
            if (row == null || row.Value.UiIcon == 0)
                continue;

            var icon = row.Value.UiIcon;
            foreach (var (alias, aliasRowId) in WaymarkNameAliases)
            {
                if (aliasRowId != rowId)
                    continue;

                entries.Add(new MacroIconEntry(MacroIconCategory.FieldMarking, alias, icon, rowId));
            }
        }

        return DeduplicateByName(entries);
    }

    private static List<MacroIconEntry> BuildClassJobIndex()
    {
        var sheet = PluginServices.Data.GetExcelSheet<ClassJob>();
        if (sheet == null)
            return [];

        var entries = new List<MacroIconEntry>();
        foreach (var row in sheet)
        {
            var name = row.Name.ToString();
            var abbreviation = row.Abbreviation.ToString();
            var icon = GetClassJobIcon(row.RowId);
            if (!IsLoadableIcon(icon))
                continue;

            if (!string.IsNullOrWhiteSpace(name))
                entries.Add(new MacroIconEntry(MacroIconCategory.ClassJob, name, icon, row.RowId, abbreviation));

            if (!string.IsNullOrWhiteSpace(abbreviation) &&
                !abbreviation.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                entries.Add(new MacroIconEntry(MacroIconCategory.ClassJob, abbreviation, icon, row.RowId, name));
            }
        }

        return DeduplicateByName(entries);
    }

    private static List<MacroIconEntry> BuildQuickChatIndex()
    {
        var sheet = PluginServices.Data.GetExcelSheet<QuickChat>();
        if (sheet == null)
            return [];

        var entries = new List<MacroIconEntry>();
        foreach (var row in sheet)
        {
            var name = row.NameAction.ToString();
            if (string.IsNullOrWhiteSpace(name) || row.Icon == 0)
                continue;

            entries.Add(new MacroIconEntry(MacroIconCategory.QuickChat, name, (uint)row.Icon, row.RowId));
        }

        return DeduplicateByName(entries);
    }

    private static List<MacroIconEntry> DeduplicateByName(List<MacroIconEntry> entries) =>
        entries
            .GroupBy(entry => Normalize(entry.Name), StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(entry => entry.Name, StringComparer.Ordinal)
            .ToList();

    internal static IReadOnlyList<MacroIconEntry> GetCategoryEntries(MacroIconCategory category)
    {
        EnsureInitialized();
        if (!Indexes.TryGetValue(category, out var entries))
            return [];

        var browsable = entries.Where(IsBrowsableEntry);
        if (category == MacroIconCategory.MacroIcon)
            return browsable.ToList();

        return browsable
            .OrderBy(entry => entry.IconId)
            .ToList();
    }

    internal static IReadOnlyList<MacroIconEntry> SearchAll(string query)
    {
        EnsureInitialized();
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var normalized = Normalize(query);
        return Indexes.Values
            .SelectMany(entries => entries)
            .Where(entry => IsBrowsableEntry(entry) && MatchesQuery(entry, normalized))
            .OrderBy(entry => Normalize(entry.Name).StartsWith(normalized, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(entry => entry.Category)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsBrowsableEntry(MacroIconEntry entry) =>
        IsLoadableIcon(entry.IconId) && !ExcludedBrowsableIconIds.Contains(entry.IconId);

    private static bool MatchesQuery(MacroIconEntry entry, string normalized)
    {
        if (Normalize(entry.Name).Contains(normalized, StringComparison.Ordinal))
            return true;

        if (!string.IsNullOrEmpty(entry.Detail)
            && Normalize(entry.Detail).Contains(normalized, StringComparison.Ordinal))
            return true;

        return false;
    }

    private static string Normalize(string value) =>
        value.Trim().ToLowerInvariant();
}
